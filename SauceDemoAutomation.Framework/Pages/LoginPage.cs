using OpenQA.Selenium;

namespace SauceDemoAutomation.Framework.Pages
{
    public class LoginPage
    {
        private readonly IWebDriver _driver;

        // Locators
        private readonly By _usernameField = By.Id("user-name");
        private readonly By _passwordField = By.Id("password");
        private readonly By _loginButton = By.Id("login-button");
        private readonly By _errorMessageContainer = By.CssSelector("*[data-test='error']");

        public LoginPage(IWebDriver driver)
        {
            _driver = driver;
        }

        public void EnterUsername(string username)
        {
            var element = _driver.FindElement(_usernameField);
            element.Clear();
            element.SendKeys(username);
        }

        public void EnterPassword(string password)
        {
            var element = _driver.FindElement(_passwordField);
            element.Clear();
            element.SendKeys(password);
        }

        public void ClickLogin()
        {
            _driver.FindElement(_loginButton).Click();
        }

        public string GetErrorMessage()
        {
            try
            {
                var element = _driver.FindElement(_errorMessageContainer);
                return element.Text;
            }
            catch (NoSuchElementException)
            {
                return string.Empty;
            }
        }

        public void Login(string username, string password)
        {
            EnterUsername(username);
            EnterPassword(password);
            ClickLogin();
        }
    }
}
