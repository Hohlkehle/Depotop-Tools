﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using HtmlAgilityPack;
using static Depotop_MC_Tools.AmazonParser;
using System.Net;
using System.Diagnostics;
using System.Xml.Linq;

namespace Depotop_MC_Tools
{
    public class TitleData
    {
        private string m_Sku;
        private string m_Auto;
        private string m_Sides;
        private string m_Oe;

        public TitleData(string sku, string auto, string sides, string oe)
        {
            m_Sku = sku;
            m_Auto = auto;
            m_Sides = sides;
            m_Oe = oe;
        }

        public string Sku { get => m_Sku; set => m_Sku = value; }
        public string Sides { get => m_Sides; set => m_Sides = value; }
        public string Auto { get => m_Auto; set => m_Auto = value; }
        public string Oe { get => m_Oe; set => m_Oe = value; }
    }

    public enum Langs
    {
        FR, EN, RU, DE, IT
    }
    public enum LangsKind
    {
        Male, Female, Middle
    }
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<string> _m_AutoList = new List<string>() { "TOYOTA", "FORD", "CHEVROLET", "NISSAN", "KIA", "MERCEDES", "BMW", "OPEL", "MAZDA", "VOLKSWAGEN", "CITROEN", "VOLVO", "SKODA", "LAND", "RENAULT", "HONDA", "MITSUBISHI", "AUDI", "JEEP", "PEUGEOT", "LEXUS", "SUZUKI", "INFINITI", "FIAT", "MINI", "ALFA", "VW", "HYUNDAI", "CHRYSLER", "SAAB", "HUMMER", "JAGUAR", "CADILLAC", "SUBARU", "DAIHATSU", "DACIA", "DODGE", "SSANGYONG", "LANCIA", "ISUZU", "SSANG", "SMART", "PORSCHE", "IVECO", "SEAT", "DAEWOO", "MCLAREN" };
        private Dictionary<string, string[]> _m_SidesLang = new Dictionary<string, string[]>()
        {
            { "avant", new string[] {"avant", "front", "передний", "vorne", "anteriore"} },
            { "arriére", new string[] {"arriére", "rear", "задний", "hinten", "posteriore"} },
            { "arriere", new string[] {"arriére", "rear", "задний", "hinten", "posteriore"} },
            { "supérieur", new string[] {"supérieur", "upper", "верхний", "oben", "superiore"} },
            { "superieur", new string[] {"supérieur", "upper", "верхний", "oben", "superiore"} },
            { "inférieur", new string[] {"inferieur", "lower", "нижний", "unten", "inferiore"} },
            { "inferieur", new string[] {"inferieur", "lower", "нижний", "unten", "inferiore"} },
            { "droite", new string[] {"droite", "right", "правый", "rechts", "destro"} },
            { "gauche", new string[] {"gauche", "left", "левый", "links", "sinistro"} },
            { "longitudinal", new string[] {"longitudinal", "longitudinal", "продольный", "entlang", "longitudinale"} },
            { "transversal", new string[] {"transversal", "transverse", "поперечный", "quer", "trasversale"} },
            { "pour", new string[] {"pour", "for", "для", "für", "per"} }
        };

        private LangsKind m_LangsKind = LangsKind.Male;
        private char m_TabChar = Convert.ToChar(9);
        private List<string> m_AutoList;
        private Dictionary<string, string[]> m_SidesLang;
        private Dictionary<string, string[]> m_Records;
        private XDocument m_Dictionary;
        public string Autos { get { return String.Join(" ", m_AutoList.ToArray()); } }

        public List<string> Titles { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            Initialize();
            InitializeImageExtract();
            InitializeImageParser();
        }

        #region Title Tratement

        private void Initialize()
        {
            m_Records = new Dictionary<string, string[]>();
            Titles = new List<string>();
        }

        private void LoadDictionary()
        {
            m_Dictionary = XDocument.Load(@"dictionary.xml");
            m_SidesLang = new Dictionary<string, string[]>();

            var query = from c in m_Dictionary.Root.Descendants("word") select c.Attributes();

            foreach (var word in query)
            {
                var w = word.ToList();
                string[] values = new string[] {
                    w.Where(a => a.Name == "fr").FirstOrDefault().Value,
                    w.Where(a => a.Name == "en").FirstOrDefault().Value,
                    w.Where(a => a.Name == "ru").FirstOrDefault().Value,
                    w.Where(a => a.Name == "de").FirstOrDefault().Value,
                    w.Where(a => a.Name == "it").FirstOrDefault().Value
                };
                m_SidesLang.Add(w.Where(a => a.Name == "name").FirstOrDefault().Value, values);
            }

            m_AutoList = new List<string>();
            var marques = m_Dictionary.Root.Descendants("marques").FirstOrDefault().Value;
            m_AutoList = (from m in marques.Split(',').AsEnumerable()
                          select m.Trim()).ToList<string>();
        }

        private void ReadInputData()
        {
            TbParserStatus.Text = "";
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            if (m_SidesLang == null || m_SidesLang.Count == 0)
            {
                LoadDictionary();
            }


            _busyIndicator.IsBusy = true;
            TbOutputData.Text = "";
            m_Records.Clear();
            Titles = TbInputTitles.Text.Split(';').ToList<string>();
            var text = TbInputData.Text;
            string[] lines = text.Split(new string[] { "\r\n" }, StringSplitOptions.None);
            PbParsingProgress.Maximum = lines.Length - 1;
            PbParsingProgress.Value = 1;
            var resultLines = new List<string>();
            Task.Factory.StartNew(() =>
            {
                var autos = String.Join("|", m_AutoList.ToArray());
                for (var i = 0; i < lines.Length; i++)
                {
                    if (lines[i] == string.Empty)
                        continue;

                    var marque = "";
                    var sides = "";
                    //var items = lines[i].Split(new[] { ';' });
                    var items = lines[i].Split(m_TabChar);
                    if (items.Length < 3)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            TbParserStatus.Text = "Input data format error!";
                        });
                        continue;
                    }
                    var pattern = @"(" + autos + ")";
                    var content = items[1].Replace("è", "e");
                    content = content.Replace("é", "e");
                    Regex rgx = new Regex(pattern, RegexOptions.IgnoreCase);
                    Match match = rgx.Match(content);

                    if (match.Success)
                    {
                        marque = match.Value.ToUpper().Trim();
                    }
                    else
                    {
                        pattern = @"(^\w+)";
                        rgx = new Regex(pattern, RegexOptions.IgnoreCase);
                        match = rgx.Match(content);
                        if (match.Success)
                        {
                            marque = match.Value.ToUpper().Trim();
                        }
                        else
                            continue;
                    }
                    pattern = "(avant|arriere|superieur|inferieur|droite|gauche|longitudinal|transversal)";
                    MatchCollection matchList = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
                    var sidesList = matchList.Cast<Match>().Select(m => m.Value.ToLower().Trim()).ToList();
                    var uniquesidesList = new HashSet<string>(sidesList);
                    sides = String.Join(" ", uniquesidesList.ToArray());

                    var title = new TitleData(items[0], marque, sides, items[2]);

                    if (m_Records.ContainsKey(items[0]))
                        items[0] += RandomString(4);
                    m_Records.Add(items[0], new[] { items[2], marque, sides });

                    var output = String.Format("{0}\t{1}\t{2}\t{3}\t{4}",
                        FormatTitle(title, Langs.FR),
                        FormatTitle(title, Langs.EN),
                        FormatTitle(title, Langs.RU),
                        FormatTitle(title, Langs.DE),
                        FormatTitle(title, Langs.IT));

                    resultLines.Add(output);

                    Dispatcher.Invoke(() =>
                    {
                        PbParsingProgress.Value = i + 1;
                    });
                }
                Dispatcher.Invoke(() =>
                {
                    PbParsingProgress.Value = 0;
                    PbParsingProgress.Maximum = 100;
                    _busyIndicator.IsBusy = false;

                    TbOutputData.Text = String.Join("\r\n", resultLines.ToArray());
                    stopwatch.Stop();
                    TbParserStatus.Text = string.Format("Elapsed Time is {0} ms", stopwatch.ElapsedMilliseconds);
                });
            });
        }

        private string FormatTitle(TitleData titleData, Langs lang)
        {
            var title = Titles[(int)lang].Trim();
            var auto = MarqueAuto(titleData.Auto);
            var sides = FormatSides(TranslatePhrase(titleData.Sides, lang));
            var pour = Translate("pour", lang);
            var refr = titleData.Oe;


            var mainTitle = String.Format("{0}{1} {2} {3} | {4}", title, sides, pour, auto, refr);
            return mainTitle;
        }

        /// <summary>
        /// Sides string with white space
        /// </summary>
        /// <param name="sides"></param>
        /// <returns></returns>
        private string FormatSides(string sides)
        {
            if (sides == string.Empty)
                return "";

            return String.Format(" {0}", sides);
        }

        private string Translate(string word, Langs lang)
        {
            if (!m_SidesLang.ContainsKey(word))
                return "UNKNOWN";
            if (lang == Langs.RU)
            {
                var ru = m_SidesLang[word][(int)lang];
                if (m_LangsKind == LangsKind.Male)
                {
                    // ...
                    return ru;
                }
                else if (m_LangsKind == LangsKind.Female)
                {
                    //нижний
                    //правый
                    ru = ru.Replace("ий", "яя");
                    ru = ru.Replace("ый", "ая");
                    return ru;
                }
                else if (m_LangsKind == LangsKind.Middle)
                {
                    ru = ru.Replace("ий", "ее");
                    ru = ru.Replace("ый", "ое");
                    return ru;
                }
            }
            return m_SidesLang[word][(int)lang];
        }

        private string TranslatePhrase(string phrase, Langs lang)
        {
            var words = new List<string>();
            foreach (var ph in phrase.Split(' '))
            {
                if (ph == string.Empty)
                    continue;
                words.Add(Translate(ph, lang));
            }

            return String.Join(" ", words.ToArray());
        }

        private string MarqueAuto(string auto)
        {
            var res = auto.ToUpper();
            if (res == "LAND")
                return "LAND ROVER";
            else if (res == "ALFA")
                return "ALFA ROMEO";
            else if (res == "SSANG")
                return "SSANG YONG";
            else
                return res;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ReadInputData();
        }

        private void RbTitlesKinds_Checked(object sender, RoutedEventArgs e)
        {
            var pressed = (System.Windows.Controls.RadioButton)sender;
            if ((string)pressed.Content == "Муж")
            {
                m_LangsKind = LangsKind.Male;

            }
            else if ((string)pressed.Content == "Жен")
            {
                m_LangsKind = LangsKind.Female;
            }
            else if ((string)pressed.Content == "Сред")
            {
                m_LangsKind = LangsKind.Middle;
            }
        }

        private void BtCopyTitlesResult_Click(object sender, RoutedEventArgs e)
        {
            if (TbOutputData.Text == "")
            {
                TbParserStatus.Text = "Нечего копировать!";
            }
            else
            {
                System.Windows.Clipboard.SetText(TbOutputData.Text);
                TbParserStatus.Text = "Скопировано в буфер обмена!";
            }
        }
        #endregion


        #region Image Extract

        public string InboxFolder { get { return TbInboxFolder.Text; } set { TbInboxFolder.Text = value; } }
        public string OutboxFolder { get { return TbOutboxFolder.Text; } set { TbOutboxFolder.Text = value; } }
        public List<string> NoExistsPhotosNames;
        private void InitializeImageExtract()
        {
            NoExistsPhotosNames = new List<string>();
        }

        private void BtnStartImageExtract_Click(object sender, RoutedEventArgs e)
        {
            NoExistsPhotosNames.Clear();
            TbImgeExtractResult.Text = "";
            _busyIndicator.IsBusy = true;
            var sku = TbImageExtractSku.Text;
            var outDirectory = OutboxFolder;
            var inputDirectory = InboxFolder;
            Task.Factory.StartNew(() =>
            {

                var lines = sku.Split(new string[] { "\r\n" }, StringSplitOptions.None).ToList<string>();

                if (lines.Count == 0)
                    return;

                if (!Directory.Exists(outDirectory))
                {
                    Dispatcher.Invoke(() =>
                    TbImgeExtractResult.Text = string.Format("Output directory not exists!"));

                    return;
                }

                var fileEntries = Directory.GetFiles(inputDirectory);
                var outputStr = "";
                var copied = new HashSet<string>();

                foreach (string f in fileEntries)
                {
                    var file = System.IO.Path.GetFileNameWithoutExtension(f);
                    var fext = System.IO.Path.GetExtension(f);
                    var fileSku = file.Remove(file.Length - 2);
                    if (lines.Contains(fileSku))
                    {
                        copied.Add(fileSku);
                        outputStr += string.Format("Copying photos for {0}...\r\n", file);
                        var destdir = System.IO.Path.Combine(outDirectory, System.IO.Path.GetFileName(f));
                        if (!File.Exists(destdir))
                            File.Copy(f, destdir);
                    }
                    else
                    {
                        outputStr += string.Format("Photos for {0} is not exists!\r\n", file);
                    }
                }
                Dispatcher.Invoke(() =>
                    {
                        NoExistsPhotosNames = lines.Except(copied).ToList();
                        TbImgeExtractResult.Text = outputStr;
                        _busyIndicator.IsBusy = false;
                    });

            });


        }

        private void BtnSelectInboxFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;

                var path = dialog.SelectedPath;

                if (!Directory.Exists(path))
                    return;

                InboxFolder = path;
            }
        }

        private void BtnSelectOutboxFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;

                var path = dialog.SelectedPath;

                if (!Directory.Exists(path))
                    return;

                OutboxFolder = path;
            }
        }


        private void BtnCopyErrorSku_Click(object sender, RoutedEventArgs e)
        {
            if (NoExistsPhotosNames.Count == 0)
            {
                TbImgeExtractResult.Text = "Нечего копировать!";
            }
            else
            {
                System.Windows.Clipboard.SetText(String.Join("\n", NoExistsPhotosNames.ToArray()));
                System.Windows.MessageBox.Show("Скопировано в буфер обмена!");
            }
        }

        #endregion
        #region Image Parser
        public string ParserOutDir { get { return TbParserOutDir.Text; } set { TbParserOutDir.Text = value; } }

        private void InitializeImageParser()
        { }

        private List<string[]> ParseInputData()
        {
            var text = TbImageParseSku.Text;
            string[] lines = text.Split(new string[] { "\r\n" }, StringSplitOptions.None);
            PbParsingProgress.Maximum = lines.Length;
            //return lines.Where(x => !string.IsNullOrEmpty(x)).Distinct() .ToArray();
            var input = new List<string[]>();
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i] == string.Empty)
                    continue;

                //var line = lines[i].Replace(@"\t", ";");
                var items = lines[i].Split(m_TabChar);
                input.Add(items);
                PbParsingProgress.Value = input.Count;
            }
            return input;
        }

        private void BtnStartParse_Click(object sender, RoutedEventArgs e)
        {
            TbParserStatus.Text = "Parsing started";
            var input = ParseInputData();
            Parser m_AmazonParser = new AmazonParser(null);
            m_AmazonParser.Initialize(/*HtmlWeb params*/);
            PbParsingProgress.Maximum = input.Count;
            var i = 0;
            foreach (var line in input)
            {
                var sku = line[0];
                var oe = line[1];
                m_AmazonParser.SearchStr = oe;
                m_AmazonParser.Search(sku, oe);

                TbParserStatus.Text = "Parsing images links for " + sku;

                if (m_AmazonParser.LastSearchResult.Count > 0)
                {
                    UpdateParserImagePrewiev(m_AmazonParser.LastSearchResult[0].PrewievUrl);
                }

                i++;
                PbParsingProgress.Value = i;
            }

            TbParserStatus.Text = "Parsing anounces links..";

            PbParsingProgress.Maximum = m_AmazonParser.ResultsCount - 1;
            foreach (var result in m_AmazonParser.Parse())
            {
                PbParsingProgress.Value = result.Index;
                UpdateParserImagePrewiev(result.ImgUrl);
            }

            var exp = System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);

            try
            {
                System.IO.File.Delete("output.csv");
                m_AmazonParser.DumpResultToCsv(System.IO.Path.Combine(exp, "output.csv"));
            }
            catch (Exception ex)
            {
                var rfileName = "output" + System.IO.Path.GetRandomFileName() + ".csv";
                m_AmazonParser.DumpResultToCsv(System.IO.Path.Combine(exp, rfileName));
                System.Windows.MessageBox.Show(ex.Message);
                System.Windows.MessageBox.Show("File saved as " + rfileName);
            }

            UpdateParserImagePrewiev(null);
            TbParserStatus.Text = "Parsing done";
            //System.Windows.MessageBox.Show("Парсиг завершен!");
        }

        private void BtnSelectParserOutDir_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;

                var path = dialog.SelectedPath;

                if (!Directory.Exists(path))
                    return;

                ParserOutDir = path;
            }
        }

        private void BtSelectParserFile_Click(object sender, RoutedEventArgs e)
        {
            var fileContent = string.Empty;
            var filePath = string.Empty;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                //openFileDialog.InitialDirectory = "c:\\";
                openFileDialog.Filter = "csv files (*.csv)|*.csv|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    //Get the path of specified file
                    TbParserFile.Text = openFileDialog.FileName;
                }
            }
        }

        private void BtStartParserDownloading_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(TbParserFile.Text))
            {
                System.Windows.MessageBox.Show("Укажите csv файл сначала!");
                return;
            }

            if (!Directory.Exists(ParserOutDir))
            {
                System.Windows.MessageBox.Show("Укажите папку для загрузки сначала!");
                return;
            }

            TbParserStatus.Text = "Start downloading...";
            PbParsingProgress.Value = 0;

            var imageDownloader = new ImageDownloader(TbParserFile.Text, ParserOutDir);
            PbParsingProgress.Maximum = imageDownloader.LoadData() - 1;
            imageDownloader.SplitByFolder = CbSplitByFolder.IsChecked == true;
            _busyIndicator.IsBusy = true;
            Task.Factory.StartNew(() =>
            {
                foreach (var result in imageDownloader.DownloadNext())
                {
                    FileSystemWatcher fw = new FileSystemWatcher(System.IO.Path.GetDirectoryName(result.File)) { EnableRaisingEvents = true };
                    fw.Created += (object ffSender, FileSystemEventArgs eargs) =>
                    {
                        Thread.Sleep(3500);
                        Dispatcher.Invoke(() =>
                        {
                            UpdateParserImagePrewiev(eargs.FullPath);
                        });
                    };

                    Dispatcher.Invoke(() =>
                    {
                        _busyIndicator.IsBusy = true;
                        PbParsingProgress.Value = result.Index;

                        TbParserStatus.Text = "Downloading images for " + result.Key;
                        _busyIndicator.IsBusy = false;
                    });
                }
                Dispatcher.Invoke(() =>
                {
                    UpdateParserImagePrewiev(null);
                    TbParserStatus.Text = "Downloading done";
                });
            });
        }

        private void UpdateParserImagePrewiev(string uri)
        {
            if (uri == null)
            {
                IParserImagePreview.Source = null;
                return;
            }
            try
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(uri, UriKind.Absolute);
                bitmap.EndInit();
                IParserImagePreview.Source = bitmap;
            }
            catch (Exception) { }
        }



        [Obsolete]
        private List<string[]> ReadParserCsvFile()
        {

            var result = new List<string[]>();
            if (!File.Exists(TbParserFile.Text))
            {
                System.Windows.MessageBox.Show("Укажите csv файл сначала!");
                return result;
            }

            PbParsingProgress.Maximum = TbParserFile.Text.Split(new string[] { "\r\n" }, StringSplitOptions.None).Length;
            using (var reader = new StreamReader(TbParserFile.Text))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(';');

                    result.Add(values);
                    PbParsingProgress.Value = result.Count;
                }
            }
            return result;
        }

        #endregion

        private static Random random = new Random();
        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

       
    }
}

/*HtmlWeb web = new HtmlWeb()
{
    AutoDetectEncoding = false,
    OverrideEncoding = Encoding.UTF8
};

web.UseCookies = true;
web.UserAgent =
       "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.11 (KHTML, like Gecko) Chrome/23.0.1271.97 Safari/537.11";


var htmlDoc = web.Load(m_SearchUrl + "71714472");

var nodes = htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 's-result-item')]");
var m_ImageLink = new List<AmazonImageLink>();
var amazonAnounces = new List<AmazonAnounce>();


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
var iurl = src.Value;

        var pattern = @"([^/]+$)";
        var content = src.Value;
        var imgName = "";
        Regex rgx = new Regex(pattern, RegexOptions.IgnoreCase);
        Match match = rgx.Match(content);


        if (match.Success)
        {
            imgName = match.Value.Trim();
            m_ImageLink.Add(new ImageLink(content));
        }
        else
        {
            // error
        }


        TbImageParseSku.Text += src.Value + "\n";
    }
}*/

/*
 [^/]+$

var rawHtml = htmlDoc.DocumentNode.OuterHtml;
var pattern = @"(avant|arriére|arriere|supérieur|superieur|inferieur|droite|gauche|longitudinal|transversal)";
var content = rawHtml;
MatchCollection matchList = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
var imageUrls = matchList.Cast<Match>().Select(m => m.Value.ToLower().Trim()).ToList();

*/

//if (node != null)
//   TbImageParseSku.Text = "Node Name: " + node.Name + "\n" + node.OuterHtml;
/*
            TbParserStatus.Text = "Downloading start";
            List<string[]> dwnList;

            try
            {
                dwnList = ReadParserCsvFile();
            }
            catch (System.IO.IOException ex)
            {
                System.Windows.MessageBox.Show(ex.Message);
                return;
            }


            if (dwnList.Count == 0)
            {
                System.Windows.MessageBox.Show("Нет файлов для загрузки!");
                return;
            }
            if (!Directory.Exists(ParserOutDir))
            {
                System.Windows.MessageBox.Show("Укажите папку для загрузки сначала!");
                return;
            }

            PbParsingProgress.Maximum = dwnList.Count;
            PbParsingProgress.Value = 0;
            using (WebClient client = new WebClient())
            {
                //client.DownloadFile(new Uri(url), @"c:\temp\image35.png");
                // OR 
                //client.DownloadFileAsync(new Uri(url), @"c:\temp\image35.png");
                _busyIndicator.IsBusy = true;
            
                var imgIndex = 0;
                var lastSku = dwnList[0][0];
                foreach (var line in dwnList)
                {
                    _busyIndicator.IsBusy = true;
                    var dir = line[0];
                    var imgurl = line[1];
                    var ext = Parser.GetFileExtensionFromUrl(imgurl);
                    
                    if (lastSku == dir)
                        imgIndex++;
                    else
                    {
                        lastSku = dir;
                        imgIndex = 1;
                    }

                    var fileName = string.Format("{0}_{1}{2}", dir, imgIndex, ext);
                    var dwnDir = (CbSplitByFolder.IsChecked == true) ? System.IO.Path.Combine(ParserOutDir, dir) : ParserOutDir;
                    var downImage = System.IO.Path.Combine(dwnDir, fileName);

                    if (!Directory.Exists(dwnDir))
                    {
                        Directory.CreateDirectory(dwnDir);
                    }

                    // Task.Factory.StartNew(() => { });
                    
                    try
                    {
                        client.DownloadFile(new Uri(imgurl), downImage);
                        UpdateParserImagePrewiev(downImage);
                        //client.DownloadFileAsync(new Uri(imgurl), downImage);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show(ex.Message);
                    }
                    PbParsingProgress.Value++;
                }

                Dispatcher.Invoke(() => { _busyIndicator.IsBusy = false; });
            }
            TbParserStatus.Text = "Downloading done";
            */
