using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Net;
using System.Diagnostics;
using System.Collections.Generic;
using libNUS.WiiU;
using System.Threading.Tasks;

namespace uTikDownloadHelper
{
    public partial class frmDownload: Form
    {
        public struct DownloadItem
        {
            public readonly byte[] ticket;
            public readonly TMD tmd;
            public readonly string name;
            public readonly NUS.UrlFilenamePair[] URLs;
            public DownloadItem(string name, TMD tmd, NUS.UrlFilenamePair[] URLs, byte[] ticket)
            {
                this.name = name;
                this.tmd = tmd;
                this.ticket = ticket;
                this.URLs = URLs;
            }
        }
        public enum DownloadType {
            None = 0,
            Game = 1,
            Update = 2,
            Both = Game | Update
        }
        public List<TitleInfo> TitleQueue = new List<TitleInfo> { };
        public DownloadType AutoDownloadType = DownloadType.None;
        public bool AutoClose = false;
        public string DownloadPath;
        public bool Downloading { get; private set; } = false;

        private Process runningProcess;
        private bool isClosing = false;
        private List<DownloadItem> DownloadQueue = new List<DownloadItem> { };
        private DownloadItem TitleItem;
        private DownloadItem UpdateItem;
        private bool TitleExists = false;
        private bool UpdateExists = false;
        private Stopwatch stopwatch1 = new LapStopwatch();
        private Stopwatch stopwatch2 = new LapStopwatch();
        private long dataDownloadedSinceLastTick = 0;
        private long dataToDownload;
        private long completeFileDataDownloaded;
        private FileInfo CurrentFile;
        public frmDownload()
        {
            InitializeComponent();
        }

        public static void OpenDownloadForm(List<TitleInfo> list)
        {
            AppDomain.CurrentDomain.DoCallBack(() =>
            {
                frmDownload frm = new frmDownload();
                frm.TitleQueue = list;
                if (list.Count > 1)
                {
                    frm.AutoClose = true;
                    switch ((new DialogTitlePatch()).ShowDialog())
                    {
                        case DialogResult.OK: // Game
                            frm.AutoDownloadType = frmDownload.DownloadType.Game;
                            break;

                        case DialogResult.Yes: // Path
                            frm.AutoDownloadType = frmDownload.DownloadType.Update;
                            break;

                        case DialogResult.No: // Both
                            frm.AutoDownloadType = frmDownload.DownloadType.Both;
                            break;

                        default:
                            return;
                    }
                    if (frm.ChooseFolder() == false)
                        return;
                }

                Program.FormContext.AddForm(frm);
            });
        }

        private void progressTimer_Tick(object sender, EventArgs e)
        {
            if (!Directory.Exists(DownloadPath))
                return;

            CurrentFile.Refresh();
            long dirSize = completeFileDataDownloaded + CurrentFile.Length;

            double progress = (double)dirSize / (double)dataToDownload;

            if (progress > 1.0)
                progress = 1.0;

            progMain.Value = Convert.ToInt32(progress * (double)progMain.Maximum);

            // Transfer speed since last tick
            long transferred = dirSize - dataDownloadedSinceLastTick;
            dataDownloadedSinceLastTick = dirSize;
            if (transferred > 0 && stopwatch1.ElapsedMilliseconds > 0)
            {
                long transferRate = Convert.ToInt64((1000.0 / (double)(stopwatch1.ElapsedMilliseconds)) * (double)transferred);
                lblTransferRate.Text = HelperFunctions.SizeSuffix(transferRate) + "ps";
            }
            stopwatch1.Restart();

            // Average speed for title
            if (stopwatch2.ElapsedMilliseconds > 0)
            {
                long avg = Convert.ToInt64((double)dirSize / ((double)stopwatch2.ElapsedMilliseconds / 1000.0));
                lblAvgTransferRate.Text = HelperFunctions.SizeSuffix(Convert.ToInt64(avg)) + "ps";

                if (avg > 0)
                {
                    // Time remaining
                    TimeSpan remainingDuration = new TimeSpan(0, 0, (int)((dataToDownload - dirSize) / avg));
                    lblTimeRemaining.Text = remainingDuration.ToString("g");
                }
            }

            
        }

        private void btnDownload_Click(object sender, EventArgs e)
        {
            DownloadQueue.Clear();
            if (chkTitle.Checked)
                DownloadQueue.Add(TitleItem);

            if (chkUpdate.Checked)
                DownloadQueue.Add(UpdateItem);

            ProcessDownloadQueue(DownloadQueue.ToArray());
        }

        private async void frmDownload_Shown(object sender, EventArgs e)
        {
            if (TitleQueue.Count == 0)
            {
                Close();
                return;
            }

            if(AutoDownloadType == DownloadType.None)
            {
                TitleInfo QueueItem = TitleQueue[0];
                this.Text = QueueItem.displayName;
                
                if (!QueueItem.isUpdate)
                {
                    try
                    {

                        TMD titleTMD = await NUS.DownloadTMD(QueueItem.titleID);
                        if (QueueItem.ticket.Length == 0)
                            QueueItem.ticket = await HelperFunctions.DownloadTitleKeyWebsiteTicket(QueueItem.titleID);

                        TitleItem = new DownloadItem(TitleQueue[0].displayName, titleTMD, await NUS.GetTitleContentURLs(titleTMD, true), TitleQueue[0].ticket);
                        TitleExists = true;
                        chkTitle.Enabled = true;
                        chkTitle.Checked = true;
                    } catch { }
                }
                try
                {
                    TMD updateTMD = await NUS.DownloadTMD(QueueItem.updateID);
                    UpdateItem = new DownloadItem(QueueItem.DisplayNameWithVersion(updateTMD.TitleVersion), updateTMD, await NUS.GetTitleContentURLs(updateTMD, true),await NUS.DownloadTicket(QueueItem.updateID));
                    UpdateExists = true;
                    chkUpdate.Enabled = UpdateExists;
                } catch { }
                btnDownload.Enabled = TitleExists || UpdateExists;
                lblDownloadingMetadata.Dispose();
            } else
            {
                var missingTitles = new List<string> { };
                var previousMax = progMain.Maximum;
                progMain.Maximum = TitleQueue.Count * (AutoDownloadType == DownloadType.Both ? 2 : 1);
                foreach(TitleInfo title in TitleQueue)
                {
                    if((AutoDownloadType & DownloadType.Game) != 0)
                    {
                        if (!title.isUpdate) {
                            try
                            {
                                if (title.ticket.Length == 0)
                                    title.ticket = await HelperFunctions.DownloadTitleKeyWebsiteTicket(title.titleID);

                                TMD titleTMD = await NUS.DownloadTMD(title.titleID);
                                DownloadQueue.Add(new DownloadItem(title.displayName, titleTMD, await NUS.GetTitleContentURLs(titleTMD, true), title.ticket));
                            }
                            catch {
                                missingTitles.Add(title.displayName);
                            }
                        }
                        progMain.Value++;
                    }

                    if((AutoDownloadType & DownloadType.Update) != 0)
                    {
                        try
                        {
                            TMD updateTMD = await NUS.DownloadTMD(title.updateID);
                            DownloadQueue.Add(new DownloadItem(title.DisplayNameWithVersion(updateTMD.TitleVersion), updateTMD, await NUS.GetTitleContentURLs(updateTMD, true), await NUS.DownloadTicket(title.updateID)));
                        } catch { }
                        progMain.Value++;
                    }
                }
                progMain.Value = 0;
                progMain.Maximum = previousMax;
                lblDownloadingMetadata.Dispose();
                ProcessDownloadQueue(DownloadQueue.ToArray());
            }
        }

        private async void ProcessDownloadQueue(DownloadItem[] items)
        {
            if(DownloadPath == null)
            {
                if (ChooseFolder() == false)
                    return;
            }

            string basePath = DownloadPath;
            Downloading = true;
            dataDownloadedSinceLastTick = 0;
            dataToDownload = 0;
            progressTimer.Enabled = true;
            int count = 0;
            string previousTitle = this.Text;
            stopwatch1.Start();
            stopwatch2.Start();
            var errors = new List<string> { };

            bool hideWget = Common.Settings.hideWget;
            bool shellExecute = Common.Settings.shellExecute;

            foreach(DownloadItem title in DownloadQueue)
            {
                count++;
                completeFileDataDownloaded = 0;
                this.Text = "(" + count + "/" + DownloadQueue.Count + ")" + title.name;
                dataDownloadedSinceLastTick = 0;
                dataToDownload = title.tmd.TitleContentSize;

                var itemPath = HelperFunctions.GetAutoIncrementedDirectory(basePath, title.name);
                Directory.CreateDirectory(itemPath);

                byte[] ticket = title.ticket;
                if (title.tmd.TitleID.ToLower()[7] != "e"[0])
                    HelperFunctions.patchTicket(ref ticket);

                File.WriteAllBytes(Path.Combine(itemPath, "title.tmd"), title.tmd.rawBytes);
                File.WriteAllBytes(Path.Combine(itemPath, "title.tik"), ticket);
                File.WriteAllBytes(Path.Combine(itemPath, "title.cert"), NUS.TitleCert);

                DownloadPath = itemPath;
                bool error = false;
                stopwatch1.Restart();
                stopwatch2.Restart();
                foreach (var url in title.URLs)
                {
                    string filePath = Path.Combine(itemPath, url.Filename);
                    File.Create(filePath).Close();
                    CurrentFile = new FileInfo(filePath);
                    lblCurrentFile.Text = url.Filename;
                    for (var i = 0; i < Common.Settings.downloadTries; i++)
                    {
                        int exitCode = await Task.Run(() => {

                            var procStIfo = new ProcessStartInfo();
                            procStIfo.FileName = Program.ResourceFiles.wget;
                            procStIfo.Arguments = HelperFunctions.escapeCommandArgument(url.URL) + " -c -O " + HelperFunctions.escapeCommandArgument(filePath);
                            procStIfo.UseShellExecute = shellExecute;
                            procStIfo.CreateNoWindow = hideWget;

                            if (shellExecute == true && hideWget == false)
                                procStIfo.WindowStyle = ProcessWindowStyle.Minimized;

                            runningProcess = new Process();
                            runningProcess.StartInfo = procStIfo;
                            runningProcess.Start();
                            runningProcess.WaitForExit();
                            return runningProcess.ExitCode;
                        });

                        if(!isClosing)
                            error = (exitCode != 0);

                        if (isClosing || exitCode == 0)
                            break;
                    }

                    if (isClosing || error)
                        break;


                    progressTimer_Tick(null, null);
                    CurrentFile.Refresh();
                    completeFileDataDownloaded += CurrentFile.Length;
                }
                if (error || isClosing)
                {
                    progressTimer.Enabled = false;
                    Directory.Delete(itemPath, true);
                    if(!isClosing)
                        errors.Add(title.name);
                }
                if (isClosing)
                    break;
            }
            progressTimer.Enabled = false;
            stopwatch1.Stop();
            stopwatch2.Stop();
            Downloading = false;
            this.Text = previousTitle;
            DownloadPath = null;
            if(errors.Count > 0)
            {
                MessageBox.Show((errors.Count > 1 ? LocalStrings.FollowingTitlesEncounteredAnErorr : LocalStrings.FollowingTitleEnounteredAnError) + "\n\n" + String.Join("\n", errors.ToArray()), LocalStrings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            } else
            {
                MessageBox.Show(LocalStrings.DownloadsCompletedSuccessfully, LocalStrings.Success);
            }
            if (AutoClose && !isClosing)
                Close();
        }

        public bool ChooseFolder()
        {
            folderDialog.SelectedPath = Common.Settings.lastPath;
            if (folderDialog.ShowDialog() != DialogResult.OK)
                return false;

            DownloadPath = folderDialog.SelectedPath;
            Common.Settings.lastPath = folderDialog.SelectedPath;
            return true;
        }

        private void frmDownload_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Downloading)
            {
                if (MessageBox.Show(LocalStrings.DownloadsInProgressCancelQuestion, LocalStrings.DownloadsInProgress, MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    isClosing = true;
                    if (runningProcess != null && runningProcess.HasExited == false)
                    {
                        runningProcess.Kill();
                    }
                }
                else
                {
                    e.Cancel = true;
                }
            }
        }

        private void frmDownload_Load(object sender, EventArgs e)
        {
            this.lblDownloadingMetadata.Location = new System.Drawing.Point(12, 38);
            this.lblDownloadingMetadata.Size = new System.Drawing.Size(474,68);
        }
    }
}
