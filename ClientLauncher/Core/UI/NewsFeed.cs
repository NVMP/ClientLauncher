using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ServiceModel.Syndication;
using System.Xml;
using System.Xml.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Net;

namespace ServerBrowser
{
    class NewsFeed
    {
        private const int MaxArticleSize = 128;

        public static string Ordinal(int number)
        {
            string suffix = String.Empty;

            int ones = number % 10;
            int tens = (int)Math.Floor(number / 10M) % 10;

            if (tens == 1)
            {
                suffix = "th";
            }
            else
            {
                switch (ones)
                {
                    case 1:
                        suffix = "st";
                        break;

                    case 2:
                        suffix = "nd";
                        break;

                    case 3:
                        suffix = "rd";
                        break;

                    default:
                        suffix = "th";
                        break;
                }
            }
            return String.Format("{0}{1}", number, suffix);
        }

        public static void Load(string url, TextBlock label)
        {
            XmlReader reader;
            try
            {
                reader = XmlReader.Create(url);
            } catch (Exception)
            {
                label.Text = "Unable to connect to NV:MP forums to retrieve announcements.";
                return;
            }

            SyndicationFeed feed = SyndicationFeed.Load(reader);
            if (feed.Items.Count() == 0)
                return;

            foreach (SyndicationItem item in feed.Items)
            {
                string text;

                var date = item.PublishDate;
                text = date.ToString("MMMM", CultureInfo.InvariantCulture);
                text += " " + Ordinal(date.Day);

                text += " - " + item.Title.Text + "\n";

                foreach (SyndicationElementExtension extension in item.ElementExtensions)
                {
                    XElement ele = extension.GetObject<XElement>();
                    if (ele.Name.LocalName == "encoded" && ele.Name.Namespace.ToString().Contains("content"))
                    {
                        string noHTML = Regex.Replace(ele.Value, @"<[^>]+>|&nbsp;", "").Trim();
                        noHTML = WebUtility.HtmlDecode(noHTML).ToString();

                        if (noHTML.Length > MaxArticleSize)
                            noHTML = noHTML.Substring(0, MaxArticleSize) + "...";

                        text += noHTML + "\n\n----\n";
                    }
                }

                label.Text += text;

            }
        }
    }
}
