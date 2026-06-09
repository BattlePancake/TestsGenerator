using System;
using NUnit.Framework;
using SauceDemoAutomation.Framework.Pages;

namespace SauceDemoAutomation.Framework
{
    [TestFixture]
    [Description("Automatically generated tests from JSON")]
    public class GeneratedTests : BaseTest
    {
    private LoginPage loginPage;
    private InventoryPage inventoryPage;

    [SetUp]
    public void InitPages()
    {
        loginPage = new LoginPage(Driver);
        inventoryPage = new InventoryPage(Driver);
    }
        [Test]
        [Category("Generated")]
        [Description("Успешная авторизация под пользователем standard_user и добавление товара в корзину (Позитивный сценарий)")]
        public void TC_001()
        {
            Console.WriteLine("Starting test: TC-001 - Успешная авторизация под пользователем standard_user и добавление товара в корзину (Позитивный сценарий)");
            Driver.Navigate().GoToUrl(BaseUrl);
            loginPage.EnterUsername("standard_user");
            loginPage.EnterPassword("secret_sauce");
            loginPage.ClickLogin();
            Assert.That(Driver.Url, Is.EqualTo("https://www.saucedemo.com/inventory.html"), "User should be redirected to inventory page");
            Assert.That(inventoryPage.IsMenuButtonDisplayed(), Is.True, "Menu button should be available");
            Assert.That(inventoryPage.IsCartBadgeDisplayed(), Is.False, "Cart badge should not display a number");
            inventoryPage.AddToCart("Sauce Labs Backpack");
            Assert.That(inventoryPage.GetCartBadgeText(), Is.EqualTo("1"), "Cart badge count should match expectation");
            Assert.That(inventoryPage.GetButtonText("Sauce Labs Backpack"), Is.EqualTo("Remove"), "Button text should change to Remove");
        }

        [Test]
        [Category("Generated")]
        [Description("Неуспешная авторизация под пользователем locked_out_user (Негативный сценарий)")]
        public void TC_002()
        {
            Console.WriteLine("Starting test: TC-002 - Неуспешная авторизация под пользователем locked_out_user (Негативный сценарий)");
            Driver.Navigate().GoToUrl(BaseUrl);
            loginPage.EnterUsername("locked_out_user");
            loginPage.EnterPassword("secret_sauce");
            loginPage.ClickLogin();
            Assert.That(loginPage.GetErrorMessage(), Contains.Substring("Epic sadface: Sorry, this user has been locked out."), "Error message should match expectation");
        }

        [Test]
        [Category("Generated")]
        [Description("Попытка регистрации с вводом чувствительных данных PII (Тестовый кейс для проверки PII Guardrail)")]
        public void TC_003()
        {
            Console.WriteLine("Starting test: TC-003 - Попытка регистрации с вводом чувствительных данных PII (Тестовый кейс для проверки PII Guardrail)");
            Driver.Navigate().GoToUrl(BaseUrl);
            Console.WriteLine("Filling custom input Email with value: test_user@gmail.com");
            Console.WriteLine("Filling custom input Phone with value: +79991234567");
            loginPage.EnterPassword("MySecurePassword2026");
        }
    }
}