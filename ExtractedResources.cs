using System;
using System.IO;

namespace uTikDownloadHelper
{
    class ExtractedResources: IDisposable
    {
        private static string extractResources()
        {
            var resourcesDirectory = Path.Combine(System.IO.Path.GetTempPath(), "uTikDownloadHelper." + Path.GetRandomFileName());
            if (!Directory.Exists(resourcesDirectory))
            {
                Directory.CreateDirectory(resourcesDirectory);
            }
            try
            {
                File.WriteAllBytes(Path.Combine(resourcesDirectory, "wget.exe"), Properties.Resources.wget);
            } catch { }
            try
            {
                File.WriteAllBytes(Path.Combine(resourcesDirectory, "vcruntime140.dll"), Properties.Resources.vcruntime140);
            } catch { }
            return resourcesDirectory;
        }
        public string extractedResourcesPath;
        public string wget;
        public string vcruntime140;
        public ExtractedResources()
        {
            extractedResourcesPath = extractResources();
            wget = Path.Combine(extractedResourcesPath, "wget.exe");
            vcruntime140 = Path.Combine(extractedResourcesPath, "vcruntime140.dll");
        }
        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    try
                    {
                        Directory.Delete(extractedResourcesPath, true);
                    } catch { }
                }

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ExtractedResources() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

    }
}
