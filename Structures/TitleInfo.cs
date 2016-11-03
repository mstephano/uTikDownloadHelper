using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Drawing;

namespace uTikDownloadHelper
{
    public struct TitleSize
    {
        public string titleID, size;
    }
    public class TitleInfo
    {
        private String _titleID;
        private String _titleKey;
        private String _name;
        private String _region;
        private String _size;
        private String _dlcKey = "";
        private ListViewItem listItem;

        public String titleID {
            get
            {
                return _titleID;
            }
            set
            {
                _titleID = value;
                UpdateListViewItem();
            }
        }
        public String titleKey {
            get
            {
                return _titleKey;
            }
            set
            {
                _titleKey = value;
                UpdateListViewItem();
            }
        }
        public String dlcKey
        {
            get
            {
                return _dlcKey;
            }
            set
            {
                _dlcKey = value;
                UpdateListViewItem();
            }
        }
        public String name
        {
            get
            {
                return _name;
            }
            set
            {
                _name = value;
                UpdateListViewItem();
            }
        }
        public String region
        {
            get
            {
                return _region;
            }
            set
            {
                _region = value;
                UpdateListViewItem();
            }
        }
        public String size {
            get
            {
                return _size;
            }
            set
            {
                _size = value;
                UpdateListViewItem();
            }
        }
        public String displayName
        {
            get
            {
                return name + (region.Length > 0 ? " (" + region + ")" : "");
            }
        }
        public bool hasTicket = false;
        

        public byte[] ticket = new byte[] { };

        public string updateID
        {
            get
            {
                var chars = titleID.ToCharArray();
                chars[7] = "e"[0];
                return new String(chars);
            }
        }
        public string gameID
        {
            get
            {
                var chars = titleID.ToCharArray();
                chars[7] = "0"[0];
                return new String(chars);
            }
        }
        public string dlcID
        {
            get
            {
                var chars = titleID.ToCharArray();
                chars[7] = "c"[0];
                return new String(chars);
            }
        }
        public bool isUpdate
        {
            get
            {
                return titleID[7] == "e"[0];
            }
        }
        public string DisplayNameWithVersion(int version, string typeName)
        {
            return displayName + " " + typeName + " v" + version;
        }
        public TitleInfo(String titleID, String titleKey, String name, String region, String size, bool hasTicket)
        {
            this.titleID = (titleID != null ? titleID.Trim().ToLower() : "");
            this.titleKey = (titleKey != null ? titleKey.Trim().ToLower() : "");

            this.name = (name != null ? name.Trim() : "");
            this.region = (region != null ? region.Trim() : "");
            this.size = (size != null ? size.Trim() : "");
            this.hasTicket = hasTicket;
        }
        private void UpdateListViewItem()
        {
            if (listItem == null)
                return;

            listItem.SubItems[0].Text = _titleID;
            listItem.SubItems[1].Text = (dlcKey.Length > 0 ? "X" : "");
            listItem.SubItems[2].Text = _name.Replace("\n", " ");
            listItem.SubItems[3].Text = _region;
            listItem.SubItems[4].Text = _size;
        }
        public ListViewItem getListViewItem()
        {
            if (listItem != null)
                return listItem;

            listItem = new ListViewItem();
            listItem.Text = titleID;
            listItem.SubItems.Add(dlcKey.Length > 0 ? "X" : "");
            listItem.SubItems.Add(name.Replace("\n", " "));
            listItem.SubItems.Add(region);
            listItem.SubItems.Add(size);
            listItem.Tag = this;
            if(!this.hasTicket)
                listItem.ForeColor = Color.Red;

            return listItem;
        }
    }
    
    public class TitleList 
    {
        private WebClient client = new WebClient();
        private String[] allowedTitleTypes = { "0000", "000c" };
        public List<TitleInfo> titles = new List<TitleInfo> { };
        

        public List<TitleInfo> filter(String name, String region)
        {
            return titles.Where(title => (title.region == region || region == "Any") && title.name.ToLower().Contains(name.ToLower())).ToList();
        }

        public void titleDataFetched(object sender, DownloadStringCompletedEventArgs e) {
            
        }

        public async Task getTitleList()
        {
            string result;

            try
            {
                result = await client.DownloadStringTaskAsync(new Uri("http://" + Common.Settings.ticketWebsite + "/json"));
            } catch
            {
                return;
            }

            titles.Clear();

            dynamic json = JArray.Parse(result);

            foreach (dynamic obj in json)
            {
                if (((String)(obj.titleID)).Length == 16)
                {
                    TitleInfo info = new TitleInfo((String)(obj.titleID), (String)(obj.titleKey), (String)(obj.name), (String)(obj.region), "", obj.ticket == "1");

                    if (info.titleID.Length == 16 && allowedTitleTypes.Contains(info.titleID.Substring(4, 4)))
                            titles.Add(info);
                }
            }

            
            var dlc = titles.Where(o => o.titleID.Substring(4, 4) == "000c").ToList();
            titles = titles.Where(o => o.titleID.Substring(4, 4) == "0000").ToList();

            foreach(TitleInfo info in dlc.ToArray())
            {
                TitleInfo[] existing = titles.Where(o => o.titleID == info.gameID).ToArray();

                if(existing.Length == 0)
                {
                    info.titleID = info.gameID;
                    info.dlcKey = info.titleKey;
                    info.titleKey = "";
                } else
                {
                    existing[0].dlcKey = info.titleKey;
                    dlc.Remove(info);
                }
            }

            titles.AddRange(dlc);

            titles = titles.OrderBy(o => o.name).ToList();

            OnListUpdated();
        }

        public event EventHandler ListUpdated;
        protected virtual void OnListUpdated()
        {
            ListUpdated?.Invoke(this, null);
        }
    }
}
