using ShopDrawing.Plugin.Core;

namespace ShopDrawing.Tests
{
    public class LayoutManagerRulesTests
    {
        [Theory]
        [InlineData("", "A")]
        [InlineData("A", "B")]
        [InlineData("Z", "AA")]
        [InlineData("AZ", "BA")]
        [InlineData("ZZ", "AAA")]
        public void NextRevision_ShouldAdvanceAlphabetically(string current, string expected)
        {
            Assert.Equal(expected, LayoutManagerRules.NextRevision(current));
        }

        [Fact]
        public void BuildPageLabel_ShouldOnlyAddSuffixForMultiPage()
        {
            Assert.Equal("W1", LayoutManagerRules.BuildPageLabel("W1", 1, 1));
            Assert.Equal("W1 (PHẦN 2/3)", LayoutManagerRules.BuildPageLabel("W1", 2, 3));
        }

        [Theory]
        [InlineData("W1/W2", "W1-W2")]
        [InlineData("TRỤC A:B", "TRỤC A-B")]
        [InlineData("  /  ", "LAYOUT")]
        [InlineData("W1 (PHẦN 1/3)", "W1 (PHẦN 1-3)")]
        public void SanitizeLayoutTabLabel_ShouldRemoveAutoCadInvalidChars(string raw, string expected)
        {
            Assert.Equal(expected, LayoutManagerRules.SanitizeLayoutTabLabel(raw));
        }

        [Theory]
        [InlineData("1:75", "1:75", "TỶ LỆ 1:75")]
        [InlineData("TỶ LỆ 1:100", "1:100", "TỶ LỆ 1:100")]
        [InlineData(" ty le 1 : 200 ", "1:200", "TỶ LỆ 1:200")]
        public void ScaleHelpers_ShouldNormalizeAndFormat(string raw, string expectedNormalized, string expectedLabel)
        {
            Assert.Equal(expectedNormalized, LayoutManagerRules.NormalizeScale(raw));
            Assert.Equal(expectedLabel, LayoutManagerRules.BuildScaleLabel(raw));
        }

        [Theory]
        [InlineData("MẶT ĐỨNG VÁCH - W1", "MẶT ĐỨNG VÁCH - W4", "MẶT ĐỨNG VÁCH - W1, W4")]
        [InlineData("SD-W1-W3", "MẶT ĐỨNG VÁCH - W4", "MẶT ĐỨNG VÁCH - W1, W3, W4")]
        [InlineData("SD-001", "MẶT ĐỨNG VÁCH - W4", "MẶT ĐỨNG VÁCH - W4")]
        [InlineData("SD-001-W4", "MẶT ĐỨNG VÁCH - W5", "MẶT ĐỨNG VÁCH - W4, W5")]
        [InlineData("MẶT ĐỨNG VÁCH - W1 (PHẦN 1/2)", "MẶT ĐỨNG VÁCH - W1", "MẶT ĐỨNG VÁCH - W1")]
        public void MergeWallNames_ShouldReturnHumanReadableDrawingTitle(string existingName, string newName, string expected)
        {
            Assert.Equal(expected, LayoutManagerRules.MergeWallNames(existingName, newName));
        }

        [Fact]
        public void GetMergedLayoutTabName_ShouldReturnSdTabNameOnly()
        {
            Assert.Equal("SD-W1-W3-W4", LayoutManagerRules.GetMergedLayoutTabName("MẶT ĐỨNG VÁCH - W1, W3, W4"));
        }

        [Theory]
        [InlineData("SD-001-W4", "MẶT ĐỨNG VÁCH - W4")]
        [InlineData("SD-W1-W3", "MẶT ĐỨNG VÁCH - W1, W3")]
        [InlineData("MẶT ĐỨNG VÁCH - W9", "MẶT ĐỨNG VÁCH - W9")]
        public void InferDrawingTitleFromLayoutTabName_ShouldRecoverHumanReadableTitle(string layoutName, string expected)
        {
            Assert.Equal(expected, LayoutManagerRules.InferDrawingTitleFromLayoutTabName(layoutName));
        }
    }
}
