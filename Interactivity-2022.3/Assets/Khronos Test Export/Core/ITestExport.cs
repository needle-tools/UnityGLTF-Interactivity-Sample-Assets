namespace Khronos_Test_Export.Core
{
    
    public interface ITestCase
    {
        string GetTestName();

        string GetTestDescription();
        void PrepareObjects(TestContext context);
        void CreateNodes(TestContext context);
    }
}