using System.ComponentModel.DataAnnotations;

namespace BrickPile.Core.Configuration
{
    /// <summary>
    /// Represents the default site configuration
    /// </summary>
    public sealed class Configuration
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>
        /// The id.
        /// </value>
        [ScaffoldColumn(false)]
        public string Id { get; private set; }

        /// <summary>
        /// Gets or sets the name of the site.
        /// </summary>
        /// <value>
        /// The name of the site.
        /// </value>
        [Required(ErrorMessage = "Required!")]
        [Display(Name = "Your website's name")]
        public string SiteName { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Configuration"/> class.
        /// </summary>
        public Configuration()
        {
            Id = "brickpile/configuration";
        }
    }
}