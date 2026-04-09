using System;

namespace ShopDrawing.Plugin.Models
{
    internal sealed class ProjectProfile
    {
        public string ProjectType { get; set; } = string.Empty;

        public string ProjectName { get; set; } = string.Empty;

        public string ProjectAddress { get; set; } = string.Empty;

        public string CustomerName { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
