using System;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace M_A_G_A.Audio
{
    public class AudioRecorder : IDisposable
    {
        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        private static extern int mciSendString(string command, StringBuilder retval, int size, IntPtr hwnd);

        private string _tempFile;
        private bool _isRecording;
        private SoundPlayer _player;

        public bool IsRecording => _isRecording;

        public void StartRecording()
        {
            if (_isRecording) return;
            _tempFile = Path.Combine(Path.GetTempPath(), $"maga_voice_{Guid.NewGuid():N}.wav");
            mciSendString("open new Type waveaudio Alias recsound", null, 0, IntPtr.Zero);
            mciSendString("set recsound time format ms", null, 0, IntPtr.Zero);
            mciSendString("record recsound", null, 0, IntPtr.Zero);
            _isRecording = true;
        }

        public byte[] StopRecording()
        {
            if (!_isRecording) return null;
            _isRecording = false;

            mciSendString("stop recsound", null, 0, IntPtr.Zero);
            mciSendString($"save recsound \"{_tempFile}\"", null, 0, IntPtr.Zero);
            mciSendString("close recsound", null, 0, IntPtr.Zero);

            // give winmm time to flush the file
            Thread.Sleep(300);

            if (File.Exists(_tempFile))
            {
                var bytes = File.ReadAllBytes(_tempFile);
                try { File.Delete(_tempFile); } catch { }
                return bytes.Length > 44 ? bytes : null; // skip header-only files
            }
            return null;
        }

        public void PlayAudio(byte[] wavData, Action onFinished = null)
        {
            StopPlayback();
            if (wavData == null || wavData.Length == 0) return;

            var thread = new Thread(() =>
            {
                try
                {
                    using (var ms = new MemoryStream(wavData))
                    {
                        var sp = new SoundPlayer(ms);
                        sp.PlaySync();
                    }
                }
                catch { }
                finally
                {
                    onFinished?.Invoke();
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        public void StopPlayback()
        {
            try { _player?.Stop(); _player = null; } catch { }
        }

        public void Dispose()
        {
            StopPlayback();
            if (_isRecording)
            {
                mciSendString("stop recsound", null, 0, IntPtr.Zero);
                mciSendString("close recsound", null, 0, IntPtr.Zero);
            }
        }
    }
}
