using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MojiCollaTool.Tests;

[TestClass]
public class PersistenceErrorMessageTests
{
    [DataTestMethod]
    [DataRow("Save", "プロジェクト保存処理に失敗しました。")]
    [DataRow("Load", "プロジェクト読込処理に失敗しました。")]
    [DataRow("WorkingCommit", "プロジェクト作業データの反映に失敗しました。")]
    public void PersistenceFailureUsesJapaneseOuterMessage(string errorKindName, string expectedMessage)
    {
        var errorKind = Enum.Parse<PersistenceErrorKind>(errorKindName);
        Assert.AreEqual(expectedMessage, MainWindow.GetPersistenceErrorMessage(errorKind));
    }

    [TestMethod]
    public void ErrorDialogHidesInternalDetailWhileLogMessageKeepsIt()
    {
        const string outerMessage = "プロジェクト読込処理に失敗しました。";
        var exception = new InvalidOperationException("corrupt image details");

        var dialogMessage = MainWindow.BuildErrorDialogMessage(outerMessage);
        var logMessage = MainWindow.BuildErrorLogMessage(outerMessage, exception);

        Assert.AreEqual(outerMessage, dialogMessage);
        Assert.IsFalse(dialogMessage.Contains(exception.Message, StringComparison.Ordinal));
        StringAssert.Contains(logMessage, exception.ToString());
    }
}
