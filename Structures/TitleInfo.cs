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

namespace uTikDownloadHelper
{
    public struct TitleSize
    {
        public string titleID, size;
    }
    public class TitleInfo
    {
        public String titleID { get; set; }
        public String titleKey { get; set; }
        public String name { get; set; }
        public String region { get; set; }
        public String size { get; set; }
        public String displayName
        {
            get
            {
                return name + " (" + region + ")";
            }
        }
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
        public bool isUpdate {
            get
            {
                return titleID[7] == "e"[0];
            }
        }
        public string NameWithVersion(int version)
        {
            return name + " Update v" + version;
        }
        public string DisplayNameWithVersion(int version)
        {
            return displayName + " Update v" + version;
        }
        public TitleInfo(String titleID, String titleKey, String name, String region, String size)
        {
            this.titleID = (titleID != null ? titleID.Trim().ToLower() : "");
            this.titleKey = (titleKey != null ? titleKey.Trim().ToLower() : "");
            this.name = (name != null ? name.Trim() : "");
            this.region = (region != null ? region.Trim() : "");
            this.size = (size != null ? size.Trim() : "");
        }
        public ListViewItem getListViewItem()
        {
            ListViewItem item = new ListViewItem();
            item.Text = titleID;
            item.SubItems.Add(name);
            item.SubItems.Add(region);
            item.SubItems.Add(size);
            item.Tag = this;
            return item;
        }
    }
    
    public class TitleList 
    {
        private WebClient client = new WebClient();
        private String[] allowedTitleTypes = {"00050000", "0005000C" };
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
                if (obj.ticket == "1" && ((String)(obj.titleID)).Length == 16)
                {
                    TitleInfo info = new TitleInfo((String)(obj.titleID), (String)(obj.titleKey), (String)(obj.name), (String)(obj.region), "");

                    if (info.titleID.Length > 8 && allowedTitleTypes.Contains(info.titleID.Substring(0, 8)))

                        if (info.name.Length > 0)
                            titles.Add(info);
                }
            }

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
