namespace UpdateFileVersionOnTag.Tests
{
    public class UpdateFileVersionOnTagTests
    {
        [Fact]
        public void VersionParse_MajorMinorBuildFormat()
        {
            Version expected = new Version(0, 0, 0, null, null);
            Version result = Version.Parse("H2M-v0.0.0");
            Assert.Equal(expected, result);
        }

        [Fact]
        public void VersionParse_MajorMinorBuildLabelFormat()
        {
            Version expected = new Version(0, 0, 0, "beta", null);
            Version result = Version.Parse("H2M-v0.0.0-beta");
            Assert.Equal(expected, result);
        }

        [Fact]
        public void VersionParse_MajorMinorBuildLabelRevisionFormat()
        {
            Version expected = new Version(0, 0, 0, "beta", 0);
            Version result = Version.Parse("H2M-v0.0.0-beta.0");
            Assert.Equal(expected, result);
        }

        [Fact]
        public void VersionParse_WithoutBuild()
        {
            Assert.Throws<FormatException>(() => Version.Parse("H2M-v0.0"));
        }

        [Fact]
        public void VersionParse_WithoutPrefixH2M()
        {
            Assert.Throws<FormatException>(() => Version.Parse("v0.0.0"));
        }
    }
}
