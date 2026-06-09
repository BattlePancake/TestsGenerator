using System;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace SauceDemoAutomation.Framework
{
    public class BaseTest
    {
        protected IWebDriver Driver;
        protected string BaseUrl = "https://www.saucedemo.com/";

        [SetUp]
        public void Setup()
        {
            var options = new ChromeOptions();
            // Headless mode is default for running in automated/headless environments
            options.AddArgument("--headless=new");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--window-size=1920,1080");

            Driver = new ChromeDriver(options);
            Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
        }

        [TearDown]
        public void Teardown()
        {
            if (Driver != null)
            {
                var outcome = TestContext.CurrentContext.Result.Outcome.Status;
                if (outcome == TestStatus.Failed || outcome == TestStatus.Inconclusive)
                {
                    TakeScreenshot(TestContext.CurrentContext.Test.Name);
                }
                Driver.Quit();
                Driver.Dispose();
            }
        }

        private void TakeScreenshot(string testName)
        {
            try
            {
                if (Driver is ITakesScreenshot ts)
                {
                    var screenshot = ts.GetScreenshot();
                    var workDir = TestContext.CurrentContext.WorkDirectory;
                    var screenshotDirectory = Path.Combine(workDir, "Screenshots");
                    Directory.CreateDirectory(screenshotDirectory);
                    
                    var fileName = $"{testName}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    var path = Path.Combine(screenshotDirectory, fileName);
                    
                    screenshot.SaveAsFile(path);
                    TestContext.AddTestAttachment(path, "Failure Screenshot");
                    Console.WriteLine($"[SCREENSHOT] Saved failure screenshot to: {path}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SCREENSHOT ERROR] Failed to take screenshot: {ex.Message}");
            }
        }
    }
}
