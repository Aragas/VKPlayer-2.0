using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace VKPlayer.AudioPlayer
{
    public class AudioDownloader
    {
        public event Action<AudioDownloader> PreDownloaded;
        public event Action<AudioDownloader> Downloaded;

        public Uri URL { get; set; }
        public SimultaneousStream Stream { get; set; }

        public bool WasStopped => !NeedsToBeStopped;
        private bool NeedsToBeStopped;


        public AudioDownloader(Uri url)
        {
            URL = url;
            Stream = new SimultaneousStream(SimultaneousStream.PositionMode.FromRead);
        }


        public async Task DownloadAsync()
        {
            using (WebResponse response = await WebRequest.Create(URL).GetResponseAsync())
            using (Stream stream = response.GetResponseStream())
            {
                bool preDownloaded = false;

                var buffer = new byte[32 * 1024]; // 32Kb chunks
                int read;
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    if (NeedsToBeStopped)
                        return;

                    await Stream.WriteAsync(buffer, 0, read);

                    if (Stream.Length > 32 * 1024 && !preDownloaded)
                    {
                        preDownloaded = true;
                        PreDownloaded?.Invoke(this);
                    }
                }

                Downloaded?.Invoke(this);
            }
        }
        public void Stop()
        {
            NeedsToBeStopped = true;
        }
    }
}
