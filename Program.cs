using System;
using System.Windows.Forms;
using System.IO;
using System.Reflection;
using System.Linq;

namespace uTikDownloadHelper
{
    class Program
    {
        public static ExtractedResources ResourceFiles = new ExtractedResources();
        public static MultiFormContext FormContext = new MultiFormContext();

        [STAThread]
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            if (Common.Settings.ticketWebsite == null) Common.Settings.ticketWebsite = "";

            if (args.Length == 0)
            {
                using (frmList frm = new frmList())
                {
                    FormContext.AddForm(frm);
                    Application.Run(FormContext);
                }
                return;
            }

            if (args[0].ToLower().EndsWith(".tmd"))
            {
                if (File.Exists(args[0]))
                {
                    frmDownload.DownloadMissing(args[0], true);
                    Application.Run(FormContext);
                }

                return;
            }
            
            string ticketInputPath = args[0];

            Byte[] ticket = { };

            if (ticketInputPath.ToLower().StartsWith("http"))
            {
                try
                {
                    ticket = (new System.Net.WebClient()).DownloadData(ticketInputPath);
                } catch (Exception e)
                {
                    MessageBox.Show(e.Message.ToString(), "Error Downloading Ticket");
                    return;
                }
            } else
            {
                ticket = File.ReadAllBytes(ticketInputPath);
            }

            if (ticket.Length < 848)
            {
                MessageBox.Show("Invalid Ticket");
            }

            using (var frm = new frmDownload()){
                string hexID = HelperFunctions.getTitleIDFromTicket(ticket);
                frm.TitleQueue.Add(new TitleInfo(hexID, "", hexID, "", ""));
                FormContext.AddForm(frm);
                Application.Run(FormContext);
            }
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            ResourceFiles.Dispose();
        }
    }
}
