using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using Telegram.Bot;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Tab;
using System.Xml.Linq;
using System.IO;
using System.Net;
using OpenQA.Selenium.DevTools.V120.Fetch;
using System.Net.Http.Headers;
using HtmlAgilityPack;
using System.Security.Policy;

namespace SRT_Ticketor
{
    public partial class Form1 : Form
    {
        #region Fields
        private string id;
        private string passwd;
        private string stDeparture, stArrival;
        private int startHour, startMin;
        private int endHour, endMin;
        private DateTime dtReserve;
        private TimeSpan tsStartTime, tsEndTime;
        private bool chkVIP, chkNormal;

        private static bool stopThread = false;
        protected System.Threading.Thread runThread = null;
        private IWebDriver webDriver = null;

        private const string telegramToken = "6749783435:AAGTBALpcBtpVzGunDflPQIb7XWGbkIwHnM";
        private const string chatID = "6428946348";
        //https://api.telegram.org/bot6749783435:AAGTBALpcBtpVzGunDflPQIb7XWGbkIwHnM/getUpdates
        //접속해 본인의 chat ID찾아 입력하기
        #endregion

        #region Properties
        public string ID { get => id; set { id = value; } }
        public string Password { get => passwd; set { passwd = value; } }
        public string DepartureStation { get => stDeparture; set { stDeparture = value; } }
        public string ArrivalStation { get => stArrival; set { stArrival = value; } }

        public int StartHour { get => startHour; set { startHour = value; } }
        public int StartMin { get => startMin; set { startMin = value; } }
        public int EndHour { get => endHour; set { endHour = value; } }
        public int EndMin { get => endMin; set { endMin = value; } }

        public DateTime ReserveDate { get => dtReserve; set { dtReserve = value; } }
        public TimeSpan ReserveStartTime { get => tsStartTime; set { tsStartTime = value; } }
        public TimeSpan ReserveEndTime { get => tsEndTime; set { tsEndTime = value; } }
        public IWebDriver WebDriver { get=>webDriver; set => webDriver = value; }

        public bool BookVIP { get => chkVIP; set { chkVIP = value; } }
        public bool BookNormal { get => chkNormal; set { chkNormal = value; } }
        #endregion
        private void btnChangeStation_Click(object sender, EventArgs e)
        {
            string dptStn = cbbDepartureStation.SelectedItem.ToString();
            string arvStn = cbbArrivalStation.SelectedItem.ToString();
            cbbDepartureStation.SelectedIndex = cbbDepartureStation.FindStringExact(arvStn);
            cbbArrivalStation.SelectedIndex = cbbArrivalStation.FindStringExact(dptStn);
        }

        public Form1()
        {
            InitializeComponent();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            stopThread = true;
        }

        private void Telegram_SendMessage(string chatID, string message)
        {
            TelegramBotClient botClient = new TelegramBotClient(telegramToken);
            botClient.SendTextMessageAsync(chatID, message);
        }

        private string GetSRTSessionInfo(CookieContainer cookie)
        {
            string sessionInfo = String.Empty;

            HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create("https://etk.srail.kr/cmc/01/selectLoginForm.do?pageId=TK0701000000"); 
            req.Method = WebRequestMethods.Http.Get; 
            req.Accept = "*/*"; 
            req.Host = "nid.naver.com"; 
            req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64; Trident/7.0; rv:11.0) like Gecko"; 
            req.Referer = "https://etk.srail.kr/cmc/01/selectLoginInfo.do?pageId=TK0701000000"; 
            req.CookieContainer = cookie;

            using (HttpWebResponse res = (HttpWebResponse)req.GetResponse())
            {
                StreamReader sr = new StreamReader(res.GetResponseStream(), Encoding.UTF8); string strResult = sr.ReadToEnd();

                try { sessionInfo = strResult; } finally { if (sr != null) sr.Close(); }
            }

            return sessionInfo;
        }

        private void button1_Click(object sender, EventArgs e)
        {

            try
            {
                CookieContainer cookie = new CookieContainer();
                //로그인 헤더
                string url = "https://etk.srail.kr/cmc/01/selectLoginInfo.do?pageId=TK0701000000";
                string responseText = string.Empty;
                ID = Regex.Replace(tbID.Text, @"(\d{3})(\d{4})(\d{4})", "$1-$2-$3");
                string PostData = string.Format("rsvTpCd=&goUrl=&from=&srchDvCd=3&srchDvNm={0}&hmpgPwdCphd={1}&saveCelNoYn=Y", ID, tbPW.Text);
                bool isLoggedIn = false;

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.Timeout = 30 * 1000; // 30초
                request.ContentType = "application/x-www-form-urlencoded";
                request.KeepAlive = true; 
                request.AllowAutoRedirect = false;
                request.CookieContainer = cookie;

                StreamWriter writer = new StreamWriter(request.GetRequestStream());
                writer.Write(PostData);
                writer.Close();

                using (HttpWebResponse resp = (HttpWebResponse)request.GetResponse())
                {
                    HttpStatusCode status = resp.StatusCode;
                    Console.WriteLine(status);  // 정상이면 "OK"

                    Stream respStream = resp.GetResponseStream();
                    using (StreamReader sr = new StreamReader(respStream))
                    {
                        responseText = sr.ReadToEnd();
                    }

                    HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
                    doc.LoadHtml(responseText);
                    var output = doc.DocumentNode.SelectSingleNode("//head/title");

                    //Console.WriteLine(output.InnerText);
                    if(output.InnerText=="") //Login 성공
                    {
                        isLoggedIn = true;
                    }
                }

                if(isLoggedIn)
                {
                    //LOGOUT
                    url = "https://etk.srail.kr/cmc/01/selectLogoutInfo.do";

                    HttpWebRequest requestLogout = (HttpWebRequest)WebRequest.Create(url);
                    requestLogout.Method = "GET";
                    requestLogout.Host = "etk.srail.kr";
                    requestLogout.CookieContainer = cookie;
                    requestLogout.Referer = "https://etk.srail.kr/main.do";
                    requestLogout.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7";

                    using (HttpWebResponse resp = (HttpWebResponse)requestLogout.GetResponse())
                    {
                        HttpStatusCode status = resp.StatusCode;
                        Console.WriteLine(status);  // 정상이면 "OK"

                        Stream respStream = resp.GetResponseStream();
                        using (StreamReader sr = new StreamReader(respStream))
                        {
                            responseText = sr.ReadToEnd();
                        }

                        HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
                        doc.LoadHtml(responseText);
                        var output = doc.DocumentNode.SelectSingleNode("//head/title");

                        //Console.WriteLine(output.InnerText);
                        if (output.InnerText == "") //Logout 성공
                        {
                            isLoggedIn = false;
                        }
                    }
                }

                Console.WriteLine(responseText);


                ////////////////
                //CookieContainer cookieContainer = new CookieContainer();


                // 1. 세션정보 가져오기           
                //string strSessionInfo = GetSRTSessionInfo(cookieContainer);
                //string url = "https://etk.srail.kr/hpg/hra/01/selectScheduleList.do?pageId=TK0101010000";  //테스트 사이트
                ////string url = "https://etk.srail.kr/main.do";
                //string responseText = string.Empty;
                //HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                //request.Method = "POST";
                //request.Timeout = 30 * 1000; // 30초
                ////request.Headers.Add("Authorization", "BASIC SGVsbG8="); // 헤더 추가 방법

                //List<string> departureStation = new List<string>();
                //List<string> arrivalStation = new List<string>();

                ////using (HttpWebResponse resp = (HttpWebResponse)request.GetResponse())
                ////{
                ////    HttpStatusCode status = resp.StatusCode;
                ////    Console.WriteLine(status);  // 정상이면 "OK"

                ////    Stream respStream = resp.GetResponseStream();
                ////    using (StreamReader sr = new StreamReader(respStream))
                ////    {
                ////        responseText = sr.ReadToEnd();
                ////    }

                ////    HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
                ////    doc.LoadHtml(responseText);
                ////    var output = doc.DocumentNode.SelectSingleNode("//*[@id=\"dptRsStnCd\"]");
                ////    for (int i = 0; i < output.ChildNodes.Count(); i++)
                ////    {
                ////        if (output.ChildNodes[i].Name == "option")
                ////        {
                ////            departureStation.Add(output.ChildNodes[i].InnerText);
                ////        }
                ////    }
                ////    *[@id = "arvRsStnCd"]
                ////}

                //Console.WriteLine(responseText);


                //HtmlWeb web = new HtmlWeb();
                //HtmlAgilityPack.HtmlDocument doc = web.Load(url);
                //var outputDpt = doc.DocumentNode.SelectSingleNode("//*[@id=\"dptRsStnCd\"]");
                //for (int i = 0; i < outputDpt.ChildNodes.Count(); i++)
                //{
                //    if (outputDpt.ChildNodes[i].Name == "option")
                //    {
                //        departureStation.Add(outputDpt.ChildNodes[i].InnerText);
                //    }
                //}

                //var outputArrival = doc.DocumentNode.SelectSingleNode("//*[@id=\"arvRsStnCd\"]");
                //for (int i = 0; i < outputArrival.ChildNodes.Count(); i++)
                //{
                //    if (outputArrival.ChildNodes[i].Name == "option")
                //    {
                //        arrivalStation.Add(outputArrival.ChildNodes[i].InnerText);
                //    }
                //}
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.GetType().ToString()} = {ex.ToString()}");
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            dtPicker.Value = DateTime.Now;

            dtStart.Value = DateTime.Now;
            dtEnd.Value = DateTime.Now.AddHours(1);

            List<string> departureStation = new List<string>();
            List<string> arrivalStation = new List<string>();

            try
            {
                string url = "https://etk.srail.kr/main.do";
                HtmlWeb web = new HtmlWeb();
                HtmlAgilityPack.HtmlDocument doc = web.Load(url);
                var outputDpt = doc.DocumentNode.SelectSingleNode("//*[@id=\"dptRsStnCd\"]");
                for (int i = 0; i < outputDpt.ChildNodes.Count(); i++)
                {
                    if (outputDpt.ChildNodes[i].Name == "option")
                    {
                        departureStation.Add(outputDpt.ChildNodes[i].InnerText);
                    }
                }
                departureStation.RemoveAt(0);//출발역 제거

                var outputArrival = doc.DocumentNode.SelectSingleNode("//*[@id=\"arvRsStnCd\"]");
                for (int i = 0; i < outputArrival.ChildNodes.Count(); i++)
                {
                    if (outputArrival.ChildNodes[i].Name == "option")
                    {
                        arrivalStation.Add(outputArrival.ChildNodes[i].InnerText);
                    }
                }
                arrivalStation.RemoveAt(0);//도착역 제거

                cbbDepartureStation.DataSource = departureStation;
                cbbArrivalStation.DataSource = arrivalStation;
            }
            catch (Exception ex) 
            { 
            }
        }

        public IWebDriver openBrowser()
        {
            string[] week_days = { "(일)", "(월)", "(화)", "(수)", "(목)", "(금)", "(토)"};
            IWebDriver driver = new ChromeDriver();
            try
            {
                driver.Url = "https://etk.srail.kr/cmc/01/selectLoginForm.do";
                // 대기 설정. (find로 객체를 찾을 때까지 검색이 되지 않으면 대기하는 시간 초단위)
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(15);

                driver.FindElement(By.XPath("//*[@id=\"srchDvCd3\"]")).Click();

                driver.FindElement(By.Id("srchDvNm03")).SendKeys(ID);
                driver.FindElement(By.Id("hmpgPwdCphd03")).SendKeys(Password);
                var element = driver.FindElement(By.XPath("//*[@id=\"login-form\"]/fieldset/div[1]/div[1]/div[4]/div/div[2]/input"));
                element.Click();
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(4);
                System.Threading.Thread.Sleep(10);

                driver.Url = "https://etk.srail.kr/hpg/hra/01/selectScheduleList.do?pageId=TK0101010000";
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);

                driver.FindElement(By.Id("dptRsStnCdNm")).Clear();
                driver.FindElement(By.Id("dptRsStnCdNm")).SendKeys(DepartureStation);

                driver.FindElement(By.Id("arvRsStnCdNm")).Clear();
                driver.FindElement(By.Id("arvRsStnCdNm")).SendKeys(ArrivalStation);

                //string date = dtReserve.ToString("yyyy'/'MM'/'dd") + week_days[(int)dtReserve.DayOfWeek];
                string date = dtReserve.ToString("yyyyMMdd");

                SelectElement dropDown = new SelectElement(driver.FindElement(By.Id("dptDt")));//.SendKeys(date);
                dropDown.SelectByValue(date);
                
                string searchHour = $"{((StartHour % 2 != 0) ? (StartHour - 1) : StartHour).ToString("00")}0000";
                driver.FindElement(By.Id("dptTm")).SendKeys(searchHour);

                element = driver.FindElement(By.XPath("//*[@id='search_top_tag']/input"));

                element.Click();
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
            }
            catch (Exception ex) 
            {
                driver.Quit();
                MessageBox.Show(ex.Message );
                return null; 
            }

            return driver;
        }

        private void btnRun_Click(object sender, EventArgs e)
        {
            ID = Regex.Replace(tbID.Text, @"(\d{3})(\d{4})(\d{4})", "$1-$2-$3");
            Password = tbPW.Text;
            DepartureStation = cbbDepartureStation.SelectedText;
            ArrivalStation = cbbArrivalStation.SelectedText;
            ReserveDate = dtPicker.Value.Date;
            StartHour = dtStart.Value.Hour;
            StartMin = dtStart.Value.Minute;
            EndHour = dtEnd.Value.Hour;
            EndMin = dtEnd.Value.Minute;
            BookVIP = cbVIPRoom.Checked;
            BookNormal = cbNormalRoom.Checked;

            ReserveStartTime = new TimeSpan(StartHour, StartMin, 0);
            ReserveEndTime = new TimeSpan(EndHour, EndMin, 0);

            stopThread = false;
            WebDriver = openBrowser();

            if (WebDriver != null)
            {
                runThread = new System.Threading.Thread(new System.Threading.ThreadStart(OnRunThreadWorker));
                runThread.Start();
            }

            //driver.Close();
        }

        protected void OnRunThreadWorker()
        {
            var table = WebDriver.FindElement(By.XPath("//*[@id=\"result-form\"]/fieldset/div[6]/table"));
            var tbody = table.FindElement(By.TagName("tbody"));
            var trs = tbody.FindElements(By.TagName("tr"));

            int idxStart = -1, idxEnd = -1;

            for (int i = 0; i < trs.Count; i++)
            {
                var tds = trs[i].FindElements(By.TagName("td"));

                DateTime tm = Convert.ToDateTime(tds[3].FindElement(By.ClassName("time")).Text);
                TimeSpan timespan = new TimeSpan(tm.Hour, tm.Minute, tm.Second);
                if (((timespan - ReserveStartTime).TotalMinutes > 0) && ((timespan - ReserveEndTime).TotalMinutes < 0))
                {
                    if (idxStart == -1) { idxStart = i; }

                    idxEnd = i;
                }
            }

            if( (idxStart==-1) && (idxEnd==-1) )
            {
                WebDriver.Quit();
                MessageBox.Show("출발시간을 재설정하세요");
                return;
            }

            bool isReserved = false;
            while (true)
            {
                try
                {
                    if (stopThread)
                    {
                        WebDriver.Quit();
                        break;
                    }

                    System.Threading.Thread.Sleep(200);

                    //var wrap = WebDriver.FindElement(By.XPath("//*[@id=\"wrap\"]/div[4]"));
                    //List<WebElement> columns = wrap.FindElements(By.XPath("./div"));
                    //IList<IWebElement> elements = WebDriver.FindElements(By.XPath("//*[@id=\"wrap\"]/div[4]"));
                    //foreach (IWebElement e in elements)
                    //{
                    //    System.Console.WriteLine(e.Text);
                    //}

                    table = WebDriver.FindElement(By.XPath("//*[@id=\"result-form\"]/fieldset/div[6]/table"));
                    if (table.Size.IsEmpty)
                    {
                        var element = WebDriver.FindElement(By.XPath("//*[@id='search_top_tag']/input"));
                        element.Click();
                        WebDriver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
                    }
                    else
                    {
                        tbody = table.FindElement(By.TagName("tbody"));
                        trs = tbody.FindElements(By.TagName("tr"));

                        for (int i = idxStart; i <= idxEnd; i++)
                        {
                            var tds = trs[i].FindElements(By.TagName("td"));
                            if (BookVIP)
                            {
                                if (tds[5].FindElement(By.TagName("a")).FindElement(By.TagName("span")).Text == "예약하기")
                                {
                                    tds[5].FindElement(By.TagName("a")).Click();
                                    isReserved = true;
                                    break;
                                }
                            }

                            if (BookNormal)
                            {
                                if (tds[6].FindElement(By.TagName("a")).FindElement(By.TagName("span")).Text == "예약하기")
                                {
                                    tds[6].FindElement(By.TagName("a")).Click();
                                    isReserved = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (isReserved)
                    {
                        if (isAlertPresent())
                        {
                            WebDriver.SwitchTo().Alert().Accept();
                        }
                        System.Threading.Thread.Sleep(2000);
                        WebDriver.Quit();
                        Telegram_SendMessage(chatID, "SRT Ticket 예약 완료!");
                        MessageBox.Show("예약 완료!");
                        break;
                    }
                    else
                    {
                        System.Threading.Thread.Sleep(1000);
                        WebDriver.Navigate().Refresh();
                        WebDriver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(6);
                    }
                    Thread.Sleep(10);
                }
                catch(NoSuchElementException ex)
                {
                    var element = WebDriver.FindElement(By.XPath("//*[@id='search_top_tag']/input"));

                    element.Click();
                }
                catch(Exception ex)
                {
                    System.Console.WriteLine($"{ex.GetType().ToString()} => {ex.Message}");
                }
            }
        }

        public bool isAlertPresent()
        {
            try
            {
                WebDriver.SwitchTo().Alert();
                return true;
            }
            catch (NoAlertPresentException Ex)
            {
                return false;
            }
        }
    }
}
