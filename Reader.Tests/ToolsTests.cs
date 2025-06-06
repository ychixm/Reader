using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reader.Business;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Reader.Tests
{
    [TestClass]
    public class ToolsTests
    {
        private const string AppSettingsFileName = "appsettings.json";
        private readonly string _testExecutionDirectory = AppDomain.CurrentDomain.BaseDirectory;
        private readonly List<string> _tempDirectoriesCreated = new List<string>();
        private bool _appSettingsCreatedByTest = false;

        private string GetFullPath(string relativePath)
        {
            return Path.Combine(_testExecutionDirectory, relativePath);
        }

        // Helper to create appsettings.json
        private void CreateTestAppSettings(string content)
        {
            File.WriteAllText(GetFullPath(AppSettingsFileName), content);
            _appSettingsCreatedByTest = true;
        }

        // Helper to delete appsettings.json
        private void DeleteTestAppSettings()
        {
            if (_appSettingsCreatedByTest && File.Exists(GetFullPath(AppSettingsFileName)))
            {
                File.Delete(GetFullPath(AppSettingsFileName));
                _appSettingsCreatedByTest = false;
            }
        }

        // Helper to create a test directory
        private void CreateTestDirectory(string dirName)
        {
            string fullPath = GetFullPath(dirName);
            Directory.CreateDirectory(fullPath);
            _tempDirectoriesCreated.Add(dirName); // Store relative path for cleanup
        }

        // Helper to delete a test directory and its contents
        private void DeleteTestDirectory(string dirName)
        {
            string fullPath = GetFullPath(dirName);
            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, true);
            }
        }

        [TestCleanup]
        public void TestCleanup()
        {
            DeleteTestAppSettings();
            foreach (var dir in _tempDirectoriesCreated)
            {
                DeleteTestDirectory(dir);
            }
            _tempDirectoriesCreated.Clear();
        }

        // --- Test Cases for Tools.GetDirectories(null) ---

        [TestMethod]
        public void GetDirectories_NullPath_ValidAppSettingsPath_ReadsFromAppSettings()
        {
            // Arrange
            string testDirName = "TestDir1_ValidApp";
            string subDirName = "SubDirA";
            CreateTestAppSettings($"{{\"DefaultPath\": \"{testDirName}\"}}");
            CreateTestDirectory(testDirName);
            CreateTestDirectory(Path.Combine(testDirName, subDirName));

            // Act
            var result = Tools.GetDirectories(null);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count, "Should find one subdirectory.");
            Assert.AreEqual(subDirName, result.First().Name, "Subdirectory name should match.");
        }

        [TestMethod]
        public void GetDirectories_NullPath_MissingAppSettingsFile_UsesBaseDirectory()
        {
            // Arrange
            DeleteTestAppSettings(); // Ensure it's not there
            string baseDirSub = "SubDirB_Base";
            // Create subdirectory directly in the base execution directory
            CreateTestDirectory(baseDirSub);

            // Act
            var result = Tools.GetDirectories(null);

            // Assert
            Assert.IsNotNull(result);
            // Check if SubDirB_Base is among the directories found in the base directory
            Assert.IsTrue(result.Any(d => d.Name == baseDirSub), $"Should find {baseDirSub} in base directory.");
        }

        [TestMethod]
        public void GetDirectories_NullPath_EmptyDefaultPathInAppSettings_UsesBaseDirectory()
        {
            // Arrange
            CreateTestAppSettings("{\"DefaultPath\": \"\"}");
            string baseDirSub = "SubDirC_BaseEmpty";
            CreateTestDirectory(baseDirSub);

            // Act
            var result = Tools.GetDirectories(null);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Any(d => d.Name == baseDirSub), $"Should find {baseDirSub} in base directory due to empty DefaultPath.");
        }

        [TestMethod]
        public void GetDirectories_NullPath_InvalidPathInAppSettings_ReturnsEmptyList()
        {
            // Arrange
            CreateTestAppSettings("{\"DefaultPath\": \"NonExistentPath_XYZ123\"}");

            // Act
            var result = Tools.GetDirectories(null);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count, "Should return an empty list for an invalid path in appsettings.");
        }

        [TestMethod]
        public void GetDirectories_NullPath_MalformedAppSettingsJson_UsesBaseDirectory()
        {
            // Arrange
            CreateTestAppSettings("{\"DefaultPath\": \"TestDirMalformed\""); // Missing closing brace
            string baseDirSub = "SubDirD_BaseMalformed";
            CreateTestDirectory(baseDirSub);

            // Act
            var result = Tools.GetDirectories(null);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Any(d => d.Name == baseDirSub), $"Should find {baseDirSub} in base directory due to malformed JSON.");
        }

        // --- Test Case for Tools.GetDirectories("some/path") ---

        [TestMethod]
        public void GetDirectories_ExplicitPathArgument_IgnoresAppSettings()
        {
            // Arrange
            string explicitDirName = "ExplicitTestDir";
            string subDirName = "SubDirE";
            CreateTestDirectory(explicitDirName);
            CreateTestDirectory(Path.Combine(explicitDirName, subDirName));

            // Create an appsettings file that *should be ignored*
            CreateTestAppSettings("{\"DefaultPath\": \"SomeOtherPathThatShouldNotBeUsed\"}");

            // Act
            var result = Tools.GetDirectories(GetFullPath(explicitDirName));

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(subDirName, result.First().Name);
        }
    }
}
