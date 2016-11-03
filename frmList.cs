using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using libNUS.WiiU;

namespace uTikDownloadHelper
{
    public partial class frmList : Form
    {
        TitleList titles = new TitleList();
        String myExe = System.Reflection.Assembly.GetEntryAssembly().Location;
        List<TitleInfo> dataSource = new List<TitleInfo> { };
        Dictionary<string, string> titleSizes = Common.Settings.cachedSizes;

        public frmList()
        {
            InitializeComponent();
        }

        private void populateList()
        {
            lstMain.Items.Clear();
            List<TitleInfo> titleList = titleList = titles.filter(txtSearch.Text, comboRegion.SelectedItem.ToString());

            foreach (TitleInfo title in titleList)
            {
                lstMain.Items.Add(title.getListViewItem());
            }
            frmList_SizeChanged(null, null);
            enableDisableDownloadButton();
        }

        private void frmList_SizeChanged(object sender, EventArgs e)
        {
            lstMain.BeginUpdate();
            lstMain.Columns[4].Width = -1;
            lstMain.Columns[2].Width = lstMain.Width - lstMain.Columns[0].Width - lstMain.Columns[1].Width - lstMain.Columns[3].Width - lstMain.Columns[4].Width - 4 - SystemInformation.VerticalScrollBarWidth;
            lstMain.EndUpdate();
        }

        private void enableDisableDownloadButton()
        {
            if (lstMain.SelectedItems.Count > 0)
            {
                btnDownload.Enabled = true;
            }
            else
            {
                btnDownload.Enabled = false;
            }
        }

        private void frmList_Load(object sender, EventArgs e)
        {
            this.lblLoading.Location = lstMain.Location;
            this.lblLoading.Size = lstMain.Size;
            btnTitleKeyCheck.Location = lstMain.Location;
            btnTitleKeyCheck.Size = lstMain.Size;

            if (Common.Settings.ticketWebsite != null && Common.Settings.ticketWebsite.Length > 0)
                btnTitleKeyCheck.Dispose();

            titles.ListUpdated += (object send, EventArgs ev) =>
            {
                comboRegion.Items.Clear();
                comboRegion.Items.Add("Any");
                foreach (TitleInfo title in titles.titles)
                {
                    if (!comboRegion.Items.Contains(title.region) && title.region.Length > 0)
                    {
                        comboRegion.Items.Add(title.region);
                    }
                }
                String lastRegion = Common.Settings.lastSelectedRegion;
                if (comboRegion.Items.Contains(lastRegion))
                {
                    comboRegion.SelectedIndex = comboRegion.Items.IndexOf(lastRegion);
                }
                else
                {
                    comboRegion.SelectedIndex = 0;
                }
                comboRegion.Enabled = true;
                txtSearch.Enabled = true;
                lstMain.Enabled = true;
                lblLoading.Dispose();
            };
        }

        public async Task getSizes(bool skipOnline = true)
        {
            foreach (TitleInfo item in titles.titles)
            {
                String contentSize;
                titleSizes.TryGetValue(item.titleID, out contentSize);
                if (contentSize == null || contentSize.Length == 0)
                {
                    if (skipOnline)
                    {
                        continue;
                    }
                    try
                    {
                        contentSize = HelperFunctions.SizeSuffix((await NUS.DownloadTMD(item.titleID)).TitleContentSize);
                    } catch {
                        contentSize = "";
                    }
                }
                item.size = contentSize;

                if (!skipOnline)
                {
                    if (titleSizes.ContainsKey(item.titleID) == false && contentSize != "")
                    {
                        titleSizes.Add(item.titleID, contentSize);

                        Common.Settings.cachedSizes = titleSizes;
                        frmList_SizeChanged(null, null);
                    }
                }
            }
            if (skipOnline)
            {
                Common.Settings.cachedSizes = titleSizes;
                frmList_SizeChanged(null, null);

                await getSizes(false);
            }
        }

        private async void frmList_Shown(object sender, EventArgs e)
        {
            if (Common.Settings.ticketWebsite != null && Common.Settings.ticketWebsite.Length > 0)
            {
                await titles.getTitleList();
                getSizes();
            }
        }

        private void comboRegion_SelectedIndexChanged(object sender, EventArgs e)
        {
            populateList();
            Common.Settings.lastSelectedRegion = comboRegion.SelectedItem.ToString();
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            if (ofdTik.ShowDialog() == DialogResult.OK && ofdTik.FileNames.Count() > 0)
            {
                List<TitleInfo> list = new List<TitleInfo> { };
                foreach (string filename in ofdTik.FileNames)
                {
                    byte[] data = File.ReadAllBytes(filename);
                    string hexID = HelperFunctions.getTitleIDFromTicket(data);
                    string basename = System.IO.Path.GetFileNameWithoutExtension(filename);
                    TitleInfo info = new TitleInfo(hexID, "", (basename.ToLower() != "title" ? hexID + " - " + basename : hexID), "", "", true);
                    info.ticket = data;
                    list.Add(info);
                }
                frmDownload.OpenDownloadForm(list);
            }
        }

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            lstMain.BeginUpdate();
            populateList();
            lstMain.EndUpdate();
        }

        private void lstMain_SelectedIndexChanged(object sender, EventArgs e)
        {
            enableDisableDownloadButton();
        }

        private void lstMain_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            handleDownload(sender, e);
        }

        private async void handleDownload(object sender, EventArgs e)
        {
            if (lstMain.SelectedItems.Count == 0)
                return;

            var list = new List<TitleInfo> { };
            foreach(ListViewItem item in lstMain.SelectedItems)
            {
                list.Add(((TitleInfo)item.Tag));
            }

            frmDownload.OpenDownloadForm(list);
        }

        private async void btnTitleKeyCheck_Click(object sender, EventArgs e)
        {
            String website = Microsoft.VisualBasic.Interaction.InputBox(LocalStrings.WhatIsTheAddress + "\n\n" + LocalStrings.TitleKeyWebsiteName + "\n\n" + LocalStrings.JustTypeTheHostname, LocalStrings.AnswerThisQuestion, "", -1, -1).ToLower();
            if (Common.getMD5Hash(website) == "d098abb93c29005dbd07deb43d81c5df")
            {
                ((Button)sender).Dispose();
                Common.Settings.ticketWebsite = website;
                await titles.getTitleList();
                getSizes();
            }
        }
    }
}
