using System;
using System.Collections.Generic;
using System.Web.Mvc;
using BrickPile.Core.DataAnnotations;

namespace BrickPile.Core.Mvc
{
    /// <summary>
    ///     Implements the default validation provider for ASP.NET MVC.
    /// </summary>
    internal class ContentTypeMetadataValidatorProvider : DataAnnotationsModelValidatorProvider
    {
        /// <summary>
        ///     Gets a list of validators.
        /// </summary>
        /// <param name="metadata">The metadata.</param>
        /// <param name="context">The context.</param>
        /// <param name="attributes">The list of validation attributes.</param>
        /// <returns>
        ///     A list of validators.
        /// </returns>
        protected override IEnumerable<ModelValidator> GetValidators(ModelMetadata metadata, ControllerContext context,
            IEnumerable<Attribute> attributes)
        {
            if (typeof (IValidatableProperty).IsAssignableFrom(metadata.ModelType))
            {
                yield return new ValidatablePropertyAdapter(metadata, context);
            }
            base.GetValidators(metadata, context, attributes);
        }
    }
}