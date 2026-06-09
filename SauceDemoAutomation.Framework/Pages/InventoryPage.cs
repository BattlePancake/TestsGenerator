using System;
using System.Linq;
using OpenQA.Selenium;

namespace SauceDemoAutomation.Framework.Pages
{
    public class InventoryPage
    {
        private readonly IWebDriver _driver;

        // Locators
        private readonly By _shoppingCartBadge = By.ClassName("shopping_cart_badge");
        private readonly By _shoppingCartLink = By.ClassName("shopping_cart_link");
        private readonly By _menuButton = By.Id("react-burger-menu-btn");

        public InventoryPage(IWebDriver driver)
        {
            _driver = driver;
        }

        private IWebElement GetProductContainer(string productName)
        {
            var containers = _driver.FindElements(By.ClassName("inventory_item"));
            foreach (var container in containers)
            {
                var nameElement = container.FindElement(By.ClassName("inventory_item_name"));
                if (nameElement.Text.Equals(productName, StringComparison.OrdinalIgnoreCase))
                {
                    return container;
                }
            }
            throw new NoSuchElementException($"Product with name '{productName}' was not found on the page.");
        }

        public void AddToCart(string productName)
        {
            var container = GetProductContainer(productName);
            var button = container.FindElement(By.TagName("button"));
            Console.WriteLine($"[DEBUG] Clicking AddToCart via JS for '{productName}'.");
            var executor = (IJavaScriptExecutor)_driver;
            executor.ExecuteScript("arguments[0].click();", button);
        }

        public void RemoveFromCart(string productName)
        {
            var container = GetProductContainer(productName);
            var button = container.FindElement(By.TagName("button"));
            Console.WriteLine($"[DEBUG] Clicking RemoveFromCart via JS for '{productName}'.");
            var executor = (IJavaScriptExecutor)_driver;
            executor.ExecuteScript("arguments[0].click();", button);
        }

        public string GetButtonText(string productName)
        {
            var container = GetProductContainer(productName);
            return container.FindElement(By.TagName("button")).Text;
        }

        public string GetCartBadgeText()
        {
            try
            {
                return _driver.FindElement(_shoppingCartBadge).Text;
            }
            catch (NoSuchElementException)
            {
                return string.Empty;
            }
        }

        public bool IsCartBadgeDisplayed()
        {
            try
            {
                return _driver.FindElement(_shoppingCartBadge).Displayed;
            }
            catch (NoSuchElementException)
            {
                return false;
            }
        }

        public bool IsMenuButtonDisplayed()
        {
            try
            {
                return _driver.FindElement(_menuButton).Displayed;
            }
            catch (NoSuchElementException)
            {
                return false;
            }
        }

        public void ClickMenu()
        {
            _driver.FindElement(_menuButton).Click();
        }
    }
}
