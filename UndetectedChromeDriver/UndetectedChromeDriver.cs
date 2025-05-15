using Newtonsoft.Json;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using System;
using System.Linq;
using OpenQA.Selenium.Support.UI;

namespace UndetectedChromeDriver
{
    public class UndetectedChromeDriver : ChromeDriver
    {
        private UndetectedChromeDriver(ChromeDriverService service, ChromeOptions options, TimeSpan commandTimeout) : base(service, options, commandTimeout) { }

        private bool _headless = false;
        private ChromeOptions _options = null;
        private ChromeDriverService _service = null;
        private Process _browser = null;
        private bool _keepUserDataDir = true;
        private string _userDataDir = null;

        public static UndetectedChromeDriver Create(ChromeOptions options = null, string userDataDir = null, string driverExecutablePath = null, string browserExecutablePath = null, int logLevel = 0, bool headless = false, bool suppressWelcome = true, bool hideCommandPromptWindow = false, TimeSpan? commandTimeout = null, Dictionary<string, object> prefs = null, Action<ChromeDriverService> configureService = null)
        {
            Patcher patcher = new Patcher(driverExecutablePath);
            patcher.PatchAll();

            if (options == null)
            {
                options = new ChromeOptions();
            }

            if (options.DebuggerAddress != null)
            {
                throw new Exception("Options is already used, please create new ChromeOptions.");
            }

            string debugHost = "127.0.0.1";
            int debugPort = FindFreePort();
            options.AddArgument($"--remote-debugging-host={debugHost}");
            options.AddArgument($"--remote-debugging-port={debugPort}");
            options.DebuggerAddress = $"{debugHost}:{debugPort}";

            bool keepUserDataDir = true;
            string userDataDirArg = options.Arguments.Select(it => Regex.Match(it, @"(?:--)?user-data-dir(?:[ =])?(.*)")).Select(it => it.Groups[1].Value).FirstOrDefault(it => !string.IsNullOrEmpty(it));

            if (userDataDirArg != null)
            {
                userDataDir = userDataDirArg;
            }
            else
            {
                if (userDataDir == null)
                {
                    keepUserDataDir = false;
                    userDataDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                }

                options.AddArgument($"--user-data-dir={userDataDir}");
            }

            string language = CultureInfo.CurrentCulture.Name;

            if (!options.Arguments.Any(it => it.Contains("--lang")))
            {
                options.AddArgument($"--lang={language}");
            }

            if (browserExecutablePath == null)
            {
                browserExecutablePath = FindChromeExecutable();

                if (browserExecutablePath == null)
                {
                    throw new Exception("Not found chrome.exe.");
                }
            }

            options.BinaryLocation = browserExecutablePath;

            if (suppressWelcome)
            {
                options.AddArguments("--no-default-browser-check", "--no-first-run");
            }

            if (headless)
            {
                options.AddArguments("--headless");
                options.AddArguments("--window-size=1920,1080");
                options.AddArguments("--start-maximized");
                options.AddArguments("--no-sandbox");
            }

            options.AddArguments($"--log-level={logLevel}");

            if (prefs != null)
            {
                HandlePrefs(userDataDir, prefs);
            }

            try
            {
                string filePath = Path.Combine(userDataDir, @"Default/Preferences");
                string json = File.ReadAllText(filePath, Encoding.GetEncoding("ISO-8859-1"));
                Regex regex = new Regex(@"(?<=exit_type"":)(.*?)(?=,)");
                string exitType = regex.Match(json).Value;

                if (exitType != "" && exitType != "null")
                {
                    json = regex.Replace(json, "null");
                    File.WriteAllText(filePath, json, Encoding.GetEncoding("ISO-8859-1"));
                }
            }
            catch
            {

            }

            string args = options.Arguments.Select(it => it.Trim()).Aggregate("", (r, it) => r + " " + (it.Contains(" ") ? $"\"{it}\"" : it));
            ProcessStartInfo info = new ProcessStartInfo(options.BinaryLocation, args);

            info.UseShellExecute = false;
            info.RedirectStandardInput = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;

            Process browser = Process.Start(info);

            if (driverExecutablePath == null)
            {
                throw new Exception("driverExecutablePath is required.");
            }

            ChromeDriverService service = ChromeDriverService.CreateDefaultService(Path.GetDirectoryName(driverExecutablePath), Path.GetFileName(driverExecutablePath));
            service.HideCommandPromptWindow = hideCommandPromptWindow;

            if (configureService != null)
            {
                configureService(service);
            }

            if (commandTimeout == null)
            {
                commandTimeout = TimeSpan.FromSeconds(60);
            }

            UndetectedChromeDriver driver = new UndetectedChromeDriver(service, options, commandTimeout.Value);

            driver._headless = headless;
            driver._options = options;
            driver._service = service;
            driver._browser = browser;
            driver._keepUserDataDir = keepUserDataDir;
            driver._userDataDir = userDataDir;

            return driver;
        }
        public void GoToUrl(string url)
        {
            if (_headless)
            {
                ConfigureHeadless();
            }

            if (HasCDCProps())
            {
                HookRemoveCDCProps();
            }

            Navigate().GoToUrl(url);
        }

        private void ConfigureHeadless()
        {
            if (ExecuteScript("return navigator.webdriver") != null)
            {
                ExecuteCdpCommand(
                    "Page.addScriptToEvaluateOnNewDocument",
                    new Dictionary<string, object>
                    {
                        ["source"] =
                        @"
                            Object.defineProperty(window, 'navigator', {
                                value: new Proxy(navigator, {
                                        has: (target, key) => (key === 'webdriver' ? false : key in target),
                                        get: (target, key) =>
                                                key === 'webdriver' ?
                                                false :
                                                typeof target[key] === 'function' ?
                                                target[key].bind(target) :
                                                target[key]
                                        })
                            });
                         "
                    });

                ExecuteCdpCommand(
                    "Network.setUserAgentOverride",
                    new Dictionary<string, object>
                    {
                        ["userAgent"] =
                        ((string)ExecuteScript(
                            "return navigator.userAgent"
                        )).Replace("Headless", "")
                    });

                ExecuteCdpCommand(
                    "Page.addScriptToEvaluateOnNewDocument",
                    new Dictionary<string, object>
                    {
                        ["source"] =
                        @"
                            Object.defineProperty(navigator, 'maxTouchPoints', {
                                    get: () => 1
                            });
                         "
                    });
            }
        }

        private bool HasCDCProps()
        {
            ReadOnlyCollection<object> props = (ReadOnlyCollection<object>)ExecuteScript(
                @"
                    let objectToInspect = window,
                        result = [];
                    while(objectToInspect !== null)
                    { result = result.concat(Object.getOwnPropertyNames(objectToInspect));
                      objectToInspect = Object.getPrototypeOf(objectToInspect); }
                    return result.filter(i => i.match(/.+_.+_(Array|Promise|Symbol)/ig))
                 ");

            return props.Count > 0;
        }

        private void HookRemoveCDCProps()
        {
            ExecuteCdpCommand(
                "Page.addScriptToEvaluateOnNewDocument",
                new Dictionary<string, object>
                {
                    ["source"] =
                    @"
                        let objectToInspect = window,
                            result = [];
                        while(objectToInspect !== null) 
                        { result = result.concat(Object.getOwnPropertyNames(objectToInspect));
                          objectToInspect = Object.getPrototypeOf(objectToInspect); }
                        result.forEach(p => p.match(/.+_.+_(Array|Promise|Symbol)/ig)
                                            &&delete window[p]&&console.log('removed',p))
                     "
                });
        }

        private static string FindChromeExecutable()
        {
            List<string> candidates = new List<string>();

            foreach (string item in new[]
            {
                "PROGRAMFILES", "PROGRAMFILES(X86)", "LOCALAPPDATA"
            })
            {
                foreach (string subitem in new[]
                {
                    @"Google\Chrome\Application",
                    @"Google\Chrome Beta\Application",
                    @"Google\Chrome Canary\Application"
                })
                {
                    string variable = Environment.GetEnvironmentVariable(item);

                    if (item == "PROGRAMFILES" && variable.Contains("86"))
                    {
                        variable = variable.Replace(" (x86)", "");
                    }

                    if (variable != null)
                    {
                        candidates.Add(Path.Combine(variable, subitem, "chrome.exe"));
                    }
                }
            }

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static int FindFreePort()
        {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                IPEndPoint localEP = new IPEndPoint(IPAddress.Any, 0);
                socket.Bind(localEP);
                localEP = (IPEndPoint)socket.LocalEndPoint;
                return localEP.Port;
            }
            finally
            {
                socket.Close();
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            try
            {
                _browser.Kill();
            }
            catch
            {

            }

            if (!_keepUserDataDir)
            {
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        Directory.Delete(_userDataDir, true);
                        break;
                    }
                    catch
                    {
                        Thread.Sleep(100);
                    }
                }
            }
        }

        private static void HandlePrefs(string userDataDir, Dictionary<string, object> prefs)
        {
            string defaultPath = Path.Combine(userDataDir, "Default");

            if (!Directory.Exists(defaultPath))
            {
                Directory.CreateDirectory(defaultPath);
            }

            Dictionary<string, object> newPrefs = new Dictionary<string, object>();
            string prefsFile = Path.Combine(defaultPath, "Preferences");

            if (File.Exists(prefsFile))
            {
                using (FileStream fs = File.Open(prefsFile, FileMode.Open, FileAccess.Read))
                {
                    using (StreamReader reader = new StreamReader(fs, Encoding.GetEncoding("ISO-8859-1")))
                    {
                        try
                        {
                            string json = reader.ReadToEnd();
                            newPrefs = Json.DeserializeData(json);
                        }
                        catch
                        {

                        }
                    }
                }
            }


            void UndotMerge(string key, object value, Dictionary<string, object> dict)
            {
                if (key.Contains("."))
                {
                    string[] split = key.Split(new char[] { '.' }, 2);
                    string k1 = split[0];
                    string k2 = split[1];

                    if (!dict.ContainsKey(k1))
                    {
                        dict[k1] = new Dictionary<string, object>();
                    }

                    UndotMerge(k2, value, dict[k1] as Dictionary<string, object>);
                    return;
                }

                dict[key] = value;
            }

            try
            {
                foreach (KeyValuePair<string, object> pair in prefs)
                {
                    UndotMerge(pair.Key, pair.Value, newPrefs);
                }
            }
            catch
            {
                throw new Exception("Prefs merge faild.");
            }

            using (FileStream fs = File.Open(prefsFile, FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (StreamWriter writer = new StreamWriter(fs, Encoding.GetEncoding("ISO-8859-1")))
                {
                    string json = JsonConvert.SerializeObject(newPrefs);
                    writer.Write(json);
                }
            }
        }

        private bool IsPageLoaded()
        {
            return (bool)ExecuteScript("return (document.readyState == \"complete\" || document.readyState == \"interactive\")");
        }

        private bool IsAjaxLoaded()
        {
            try
            {
                return (bool)ExecuteScript("var result = true; try { result = (typeof jQuery != 'undefined') ? jQuery.active == 0 : true } catch (e) {}; return result;");
            }
            catch
            {
                return false;
            }
        }

        public bool IsPageReady()
        {
            return IsPageLoaded() && IsAjaxLoaded();
        }

        public void MaximizeWindow()
        {
            Manage().Window.Maximize();
        }

        public bool IsElementLoaded(IWebElement element)
        {
            try
            {
                if (element == null)
                {
                    return false;
                }

                return element.Displayed && element.Enabled;
            }
            catch
            {
                return false;
            }
        }

        private Func<IWebDriver, IWebElement> ElementIsClickable(By locator)
        {
            return driver =>
            {
                IWebElement element = driver.FindElement(locator);
                return IsPageReady() && (element != null && element.Displayed && element.Enabled) ? element : null;
            };
        }

        public IWebElement FindLoadedElement(By locator)
        {
            WebDriverWait wait = new WebDriverWait(this, TimeSpan.FromDays(1));
            IWebElement clickableElement = wait.Until(ElementIsClickable(locator));
            return clickableElement;
        }

        public IWebElement FindLoadedElementByTagValue(string tagName, string tagValue)
        {
            return FindLoadedElement(By.CssSelector($"[{tagName}=\"{tagValue}\"]"));
        }
    }
}