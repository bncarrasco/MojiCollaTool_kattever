using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MojiCollaTool.Tests
{
    [TestClass]
    public class ForkBrandingTests
    {
        [TestMethod]
        public void AssemblyMetadataUsesForkDisplayName()
        {
            var assembly = typeof(MainWindow).Assembly;

            Assert.AreEqual(ProductIdentity.DisplayName, assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title);
            Assert.AreEqual(ProductIdentity.DisplayName, assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product);
            StringAssert.Contains(assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? string.Empty, "非公式フォーク");
        }

        [TestMethod]
        public void ForkIdentityContainsRequiredNotices()
        {
            StringAssert.Contains(ProductIdentity.ForkNotice, "非公式フォーク");
            StringAssert.Contains(ProductIdentity.ForkNotice, "原作者とは無関係");
            StringAssert.Contains(ProductIdentity.ContactNotice, "原作者へ送らない");
        }
    }
}
