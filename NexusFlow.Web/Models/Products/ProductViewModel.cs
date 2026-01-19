using System.ComponentModel.DataAnnotations;

namespace NexusFlow.Web.Models.Products
{
    public class ProductViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Product Code")]
        public string Code { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public string Category { get; set; }

        [Range(0, 10000)]
        public decimal Price { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
