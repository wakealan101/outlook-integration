using HigherKnowledge_addin.Properties;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Office = Microsoft.Office.Core;
using Outlook = Microsoft.Office.Interop.Outlook;
using System.Runtime.Serialization.Json;

// TODO:  Follow these steps to enable the Ribbon (XML) item:

// 1: Copy the following code block into the ThisAddin, ThisWorkbook, or ThisDocument class.

//  protected override Microsoft.Office.Core.IRibbonExtensibility CreateRibbonExtensibilityObject()
//  {
//      return new Ribbon1();
//  }

// 2. Create callback methods in the "Ribbon Callbacks" region of this class to handle user
//    actions, such as clicking a button. Note: if you have exported this Ribbon from the Ribbon designer,
//    move your code from the event handlers to the callback methods and modify the code to work with the
//    Ribbon extensibility (RibbonX) programming model.

// 3. Assign attributes to the control tags in the Ribbon XML file to identify the appropriate callback methods in your code.  

// For more information, see the Ribbon XML documentation in the Visual Studio Tools for Office Help.


namespace HigherKnowledge_addin
{
    [ComVisible(true)]
    public class Ribbon1 : Office.IRibbonExtensibility
    {
        private Office.IRibbonUI ribbon;

        public Ribbon1()
        {
        }
        
        Template template = null;

        #region IRibbonExtensibility Members

        public string GetCustomUI(string ribbonID)
        {
            return GetResourceText("HigherKnowledge_addin.Ribbon1.xml");
        }

        #endregion

        #region Ribbon Callbacks
        //Create callback methods here. For more information about adding callback methods, visit http://go.microsoft.com/fwlink/?LinkID=271226

        public void Ribbon_Load(Office.IRibbonUI ribbonUI)
        {
            this.ribbon = ribbonUI;
        }

        public void onViewButton(Office.IRibbonControl control)
        {
           
            try
            {
                if (template == null)
                    fetch();
                
                string dis = "Subject : \n\n" + template.subject + "\n\nCC :\n\n" + template.cc + "\n\nBody :\n\n" + getBody();
                MessageBox.Show(dis);
            }
            catch(NullReferenceException e)
            {
                MessageBox.Show("Could not load the template");
            }
        }

        public void OnReplyButton(Office.IRibbonControl control)
        {
            //Send opening draft to google analytics here
            
            var context = control.Context;
            if(context is Outlook.Explorer)
            {
                var explorer = context as Outlook.Explorer;
                var selections = explorer.Selection;
                foreach(var child in selections)
                {
                    if(child is Outlook.MailItem)
                    {
                        var mail = child as Outlook.MailItem;
                        send(mail);
                        break;
                    }
                }
            }

            else if(context is Outlook.Inspector)
            {
                var ins = context as Outlook.Inspector;
                if (ins.CurrentItem is Outlook.MailItem)
                {
                    var mail = ins.CurrentItem as Outlook.MailItem;
                    send(mail);
                    ins.Close(Outlook.OlInspectorClose.olSave);
                }

                else
                    MessageBox.Show("Cannot perform the action in the current context");
            }
            else
            {
                MessageBox.Show("Cannot perform the action in the current context");
            }
        }
        private bool sent = false;

        private void send(Outlook.MailItem mail)
        {
            ga("clicked");
            string name = mail.Sender.Address;
            //DialogResult result = MessageBox.Show("Send HK response to " + name,"Confirmation...", MessageBoxButtons.YesNo);
            //if (result == DialogResult.Yes)
            {
               // MessageBox.Show(ThisAddIn.User);
                Outlook.MailItem reply = mail.Reply();
                try
                {
                   if (template == null)
                    {
                        fetch();
                    }
                    sent = false;
                    reply.CC = template.cc;                 
                    reply.HTMLBody = getBody();
                    reply.Subject = template.subject;
                    reply.Display();
                    ((Outlook.ItemEvents_10_Event)reply).Send += Ribbon1_Send;
                    ((Outlook.ItemEvents_10_Event)reply).Close += Ribbon1_Close;
                }
                catch(Exception)
                {
                    MessageBox.Show("Could not Reply");
                }
            }
        }

        

        private void Ribbon1_Close(ref bool Cancel)
        {
            if(!Cancel)
            {
                if(!sent)
                {
                    //implement google analytics here
                    ga("closed wihtout sending");
                }
            }
        }

        private void Ribbon1_Send(ref bool Cancel)
        {
            if(!Cancel)
            {
                MessageBox.Show("Email sent");
                sent = true;
                ga("sent");
                //implement google analytics here.
            }
        }



        #endregion

        #region Helpers

        private static string GetResourceText(string resourceName)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            string[] resourceNames = asm.GetManifestResourceNames();
            for (int i = 0; i < resourceNames.Length; ++i)
            {
                if (string.Compare(resourceName, resourceNames[i], StringComparison.OrdinalIgnoreCase) == 0)
                {
                    using (StreamReader resourceReader = new StreamReader(asm.GetManifestResourceStream(resourceNames[i])))
                    {
                        if (resourceReader != null)
                        {
                            return resourceReader.ReadToEnd();
                        }
                    }
                }
            }
            return null;
        }

        public Bitmap getDone(Office.IRibbonControl control)
        {
            return Resources.done;
        }

        public Bitmap getView(Office.IRibbonControl control)
        {
            return Resources.View;
        }

        private void fetch()
        {
            string raw = "https://raw.githubusercontent.com/";
            string path = "higherknowledge/outlook-integration/master/templates/";
            
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(raw + path + ThisAddIn.User);
                var res = (HttpWebResponse)request.GetResponse();
                var stream = res.GetResponseStream();
                StreamReader reader = new StreamReader(stream);
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(Template));
                object obj = ser.ReadObject(stream);
                template = obj as Template;
                stream.Close();
                res.Close();
            }

            catch (Exception e)
            {
                MessageBox.Show("Unable to fetch the template...\n" + e);
            }
        } 

        private string getBody()
        {
            string temp = "";
            foreach (var s in template.body)
                temp += s + "<br/><br/>";
            return temp;
        }

        private void ga(string action)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://www.google-analytics.com/collect");
                request.Method = "POST";
                string data = "v = 1 & t=event & tid=UA-81367328-1 & cid=1";
                data += "&ec=" + ThisAddIn.User + "&el=Used Add in" + "&ev = 1";
                data += "&ea=" + action;
                data = WebUtility.UrlEncode(data);
                var db = Encoding.ASCII.GetBytes(data);
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(db, 0, db.Length);
                    stream.Close();
                }
                var response = (HttpWebResponse)request.GetResponse();
                MessageBox.Show(response.StatusCode + "");
            }
            catch(Exception e)
            {
                MessageBox.Show(e.ToString());
            }
        }
        #endregion
    }
}

