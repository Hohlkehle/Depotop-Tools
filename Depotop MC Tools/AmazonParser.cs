﻿using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Depotop_MC_Tools
{
    public class AmazonParser : Parser
    {
        private string m_DownloadDir;

        public string DownloadDir { get => m_DownloadDir; set => m_DownloadDir = value; }
        public string SearchUrl { get => m_SearchUrl; set => m_SearchUrl = value; }


        private string m_SearchUrl = "https://www.amazon.com/s?k=";

        public AmazonParser(string downloadDir) : base()
        {
            m_DownloadDir = downloadDir;
        }

        public enum ImageSize
        {
            //_SL1500_
            //_AC_UY218_ML3_
            //_SX355_
            //_SX425_
            //_SX450_
            //_SX466_
            //_SX522_
            //_SX569_
            //_SX679_

            Preview, // _AC_UY218_ML3_
            Original, // 
            FullSize // _SL1500_
        }

        public class AmazonImageLink : ImageLink
        {

            private string m_Src;
            private string m_ServerPath;
            private string m_Name;
            private string m_Size;
            private string m_Ext;
            public AmazonImageLink(string src)
            {
                m_Src = src;

                var pattern = @"([^/]+$)";
                var content = src;
                var imgName = "";
                Regex rgx = new Regex(pattern, RegexOptions.IgnoreCase);
                Match match = rgx.Match(content);

                if (match.Success)
                {
                    imgName = match.Value.Trim(); // 71nCl5DivdL._AC_UY218_ML3_.jpg
                    m_Ext = Path.GetExtension(imgName);
                    m_Name = imgName.Split('.')[0];
                    m_Size = imgName.Split('.')[1];
                    m_ServerPath = src.Split(new string[] { imgName }, StringSplitOptions.None)[0];
                }
                else
                {
                    // error
                }

            }
            public AmazonImageLink(AmazonImageLink link)
            {
                if (link == null)
                    return;
                m_Src = link.Src;
                m_Ext = link.Ext;
                m_Name = link.Name;
                m_Size = link.Size;
                m_ServerPath = link.ServerPath;
            }
            public string Src { get => m_Src; set => m_Src = value; }
            public string ServerPath { get => m_ServerPath; set => m_ServerPath = value; }
            public string Name { get => m_Name; set => m_Name = value; }
            public string FullName { get => m_Name + "." + m_Size + m_Ext; }
            public string Size { get => m_Size; set => m_Size = value; }
            public string Ext { get => m_Ext; set => m_Ext = value; }
            public override string Url { get { return ServerPath + FullName; } }
            public string ReBuild(ImageSize size)
            {
                var s = "_AC_UY218_ML3_";
                if (size == ImageSize.FullSize)
                {
                    s = "_SL1500_";

                }
                else
                {

                }
                Size = s;
                //https://m.media-amazon.com/images/I/71nCl5DivdL._AC_UY218_ML3_.jpg
                return string.Format("{0}{1}.{2}{3}", ServerPath, m_Name, Size, Ext);
            }
        }

        public class AmazonAnounce : Anounce
        {
            private string m_Url;
            public AmazonAnounce(string url, AmazonImageLink prewievImage)
            {
                m_Url = url;
                ImageLinks = new List<ImageLink>();
                ImageLinks.Add(prewievImage);
            }

            public AmazonAnounce(string url, List<ImageLink> images)
            {
                m_Url = url;
                ImageLinks = images;
            }

            public string Url { get => m_Url; set => m_Url = value; }
            //public List<AmazonImageLink> ImageLinks { get => m_ImageLinks; set => m_ImageLinks = value; }
            public AmazonImageLink PrewievImage
            {
                get
                {
                    if (ImageLinks.Count > 0) return (AmazonImageLink)ImageLinks[0]; return null;
                }
            }

            public override void LoadAnounceData(HtmlAgilityPack.HtmlWeb htmlWeb)
            {
                var htmlDoc = htmlWeb.Load(Url);
                //var nodes = htmlDoc.DocumentNode.SelectNodes(".//");
                //images/I/([^.]+)[.]_SL
                //ImageBlockATF
                var html = htmlDoc.DocumentNode;


                var pattern = @"images/I/([^.]+)[.]_SL";

                MatchCollection matchList = Regex.Matches(html.InnerText, pattern, RegexOptions.IgnoreCase);
                var resList = matchList.Cast<Match>().Select(m => m.Value.ToLower().Trim()).ToList();
                var uniquesidesList = new HashSet<string>(resList);
                foreach (var iName in uniquesidesList)
                {
                    var link = new AmazonImageLink(PrewievImage);
                    link.Name = iName;
                    link.ReBuild(ImageSize.FullSize);
                    ImageLinks.Add(link);
                }
            }
        }

        public override void Initialize()
        {
            base.Initialize();

        }


        public override void Search(string id, string searchStr)
        {
            var htmlDoc = m_HtmlWeb.Load(SearchUrl + searchStr);

            var nodes = htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 's-result-item')]");
            var m_ImageLink = new List<AmazonImageLink>();
            var amazonAnounces = new List<Anounce>();

            if (nodes != null)
            {
                foreach (HtmlNode item in nodes)
                {
                    var postUrl = "";
                    var postNode = item.SelectSingleNode(".//a[contains(@class, 'a-link-normal')]");
                    if (postNode != null)
                    {
                        var pn = postNode.Attributes.FirstOrDefault(u => u.Name == "href");
                        postUrl = pn.Value;
                    }

                    var iurlNode = item.SelectSingleNode(".//img/@src");
                    if (iurlNode == null)
                        continue;

                    var src = iurlNode.Attributes.FirstOrDefault(u => u.Name == "src");

                    var anounce = new AmazonAnounce(src.Value, new AmazonImageLink(src.Value));

                    amazonAnounces.Add(anounce);

                }
            }
            m_searchResults.Add(id, amazonAnounces);
        }
    }
}
