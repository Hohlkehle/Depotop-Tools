﻿using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Depotop_MC_Tools
{
    public class Parser
    {
        public class ImageLink
        {
            public virtual string Url { get; }
        }
        public class ParsedImage
        {
            private string m_Sku;
            private string m_Url;
            private string m_Oe;
            public ParsedImage(string sku, string url, string oe)
            {
                m_Sku = sku;
                m_Url = url;
                m_Oe = oe;
            }

            public string Oe { get => m_Oe; set => m_Oe = value; }
            public string Url { get => m_Url; set => m_Url = value; }
            public string Sku { get => m_Sku; set => m_Sku = value; }
        }
        protected HtmlWeb m_HtmlWeb;
        protected string m_SearchStr = "";
        protected string m_UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.11 (KHTML, like Gecko) Chrome/23.0.1271.97 Safari/537.11";
        protected List<string> m_ImagesUrl;
        protected Dictionary<string, List<Anounce>> m_searchResults;
        public Parser() { m_ImagesUrl = new List<string>(); }
        public string SearchStr { get => m_SearchStr; set => m_SearchStr = value; }
        public string UserAgent { get => m_UserAgent; set => m_UserAgent = value; }
        public List<string> ImagesUrl { get => m_ImagesUrl; set => m_ImagesUrl = value; }
        public Dictionary<string, List<Anounce>> SearchResults { get => m_searchResults; set => m_searchResults = value; }

        public virtual void Parse()
        {
            foreach (KeyValuePair<string, List<Anounce>> kvp in m_searchResults)
            {
                foreach (var a in kvp.Value)
                {
                    a.LoadAnounceData(m_HtmlWeb);
                }
            }
        }
        public virtual void Search(string id, string searchStr) { }
        public virtual void Initialize()
        {
            m_searchResults = new Dictionary<string, List<Anounce>>();

            m_HtmlWeb = new HtmlWeb()
            {
                AutoDetectEncoding = false,
                OverrideEncoding = Encoding.UTF8
            };

            m_HtmlWeb.UseCookies = true;
            m_HtmlWeb.UserAgent = UserAgent;
        }

        public void DumpResultToCsv(string path)
        {
            var csv = new StringBuilder();

            foreach (KeyValuePair<string, List<Anounce>> kvp in m_searchResults)
            {
                foreach (var a in kvp.Value)
                {
                    if (a.ImageLinks == null)
                        continue;
                    foreach (var i in a.ImageLinks)
                    {
                        var newLine = string.Format("{0};{1}", kvp.Key, i.Url);
                        csv.AppendLine(newLine);
                    }
                }
            }

            File.WriteAllText(path, csv.ToString());
        }
    }
}
