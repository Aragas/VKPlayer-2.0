using System;
using System.IO;
using System.Net;
using System.Threading;

namespace VKPlayer.AudioPlayer
{
    public class AudioDownloader
    {
        public event Action<SimultaneousStream> PreDownloaded;
        public event Action<SimultaneousStream> Downloaded;

        public bool WasStopped => !NeedsToBeStopped;
        private bool NeedsToBeStopped;

        private Thread _downloader;


        public void Download(Uri url)
        {
            if (_downloader != null && _downloader.IsAlive)
            {
                StopAnyDownload();

                Thread.Sleep(50);
                if (_downloader.IsAlive)
                    _downloader.Abort();
            }

            _downloader = new Thread(() => Downloader(url));
            _downloader.Start();
        }
        private void Downloader(Uri url)
        {
            var downloadedStream = new SimultaneousStream(SimultaneousStream.PositionMode.FromRead);

            using (WebResponse response = ((HttpWebRequest) WebRequest.Create(url)).GetResponse())
            using (Stream stream = ((HttpWebResponse) response).GetResponseStream())
            {
                bool preDownloaded = false;

                var buffer = new byte[16 * 1024]; // 16Kb chunks
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (NeedsToBeStopped)
                    {
                        NeedsToBeStopped = false;
                        return;
                    }

                    downloadedStream.Write(buffer, 0, read);

                    if (downloadedStream.Length > 64 * 1024 && !preDownloaded)
                    {
                        preDownloaded = true;
                        new Thread(() => PreDownloaded?.Invoke(downloadedStream)).Start();
                    }
                }
                if(!preDownloaded)
                {
                    preDownloaded = true;
                    new Thread(() => PreDownloaded?.Invoke(downloadedStream)).Start();
                }

                Downloaded?.Invoke(downloadedStream);
            }
        }

        public void StopAnyDownload()
        {
            NeedsToBeStopped = true;
        }
    }
}
