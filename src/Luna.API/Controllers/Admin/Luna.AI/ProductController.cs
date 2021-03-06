using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Luna.Clients.Azure.Auth;
using Luna.Clients.Controller;
using Luna.Clients.Exceptions;
using Luna.Clients.Logging;
using Luna.Data.DataContracts.Luna.AI;
using Luna.Data.Entities;
using Luna.Data.Entities.Luna.AI;
using Luna.Services.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Luna.API.Controllers.Admin
{
    /// <summary>
    /// API controller for product resource.
    /// </summary>
    [ApiController]
    [Authorize]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("api")]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly IPublisherService _publisherService;

        private readonly ILogger<ProductController> _logger;

        /// <summary>
        /// Constructor that uses dependency injection.
        /// </summary>
        /// <param name="productService">The service to inject.</param>
        /// <param name="logger">The logger.</param>
        public ProductController(IProductService productService, IPublisherService publisherService, ILogger<ProductController> logger)
        {
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _publisherService = publisherService ?? throw new ArgumentNullException(nameof(publisherService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets all products.
        /// </summary>
        /// <returns>HTTP 200 OK with product JSON objects in response body.</returns>
        [HttpGet("products")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> GetAllAsync()
        {
            AADAuthHelper.VerifyUserAccess(this.HttpContext, _logger, true);
            _logger.LogInformation("Get all products.");
            return Ok(await _productService.GetAllAsync());
            
        }

        /// <summary>
        /// Get a product.
        /// </summary>
        /// <param name="productName">The name of the product to get.</param>
        /// <returns>HTTP 200 OK with product JSON object in response body.</returns>
        [HttpGet("products/{productName}", Name = nameof(GetAsync) + nameof(Product))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> GetAsync(string productName)
        {
            AADAuthHelper.VerifyUserAccess(this.HttpContext, _logger, true);
            _logger.LogInformation($"Get product {productName}");
            return Ok(await _productService.GetAsync(productName));
        }

        [HttpGet("publisher", Name = nameof(GetAsync) + nameof(Publisher))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> GetPublisherAsync()
        {
            _logger.LogInformation($"Get publisher info");
            return Ok(await _publisherService.GetAsync());
        }

        [HttpGet("productTypes")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> GetProductTypes()
        {
            List<ProductType> productTypeList = new List<ProductType>();

            productTypeList.Add(new ProductType() { DisplayName = "Machine Learning Projects", Id = "TYOM" });
            productTypeList.Add(new ProductType() { DisplayName = "Deployed Model Service Endpoints", Id = "RTP" });

            return Ok(productTypeList);

        }

        [HttpGet("hostTypes")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> GetHostTypes()
        {
            List<ProductType> productTypeList = new List<ProductType>();

            productTypeList.Add(new ProductType() { DisplayName = "SaaS and Selfhost", Id = "Both" });
            productTypeList.Add(new ProductType() { DisplayName = "SaaS", Id = "SaaS" });
            productTypeList.Add(new ProductType() { DisplayName = "Selfhost", Id = "BYOC" });

            return Ok(productTypeList);

        }

        /// <summary>
        /// Creates or updates an product.
        /// </summary>
        /// <param name="productName">The name of the product to update.</param>
        /// <param name="product">The updated product object.</param>
        /// <returns>HTTP 201 CREATED with URI to created resource in response header.</returns>
        /// <returns>HTTP 200 OK with updated product JSON objects in response body.</returns>
        [HttpPut("products/{productName}")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> CreateOrUpdateAsync(string productName, [FromBody] Product product)
        {
            AADAuthHelper.VerifyUserAccess(this.HttpContext, _logger, true);
            if (product == null)
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposePayloadNotProvidedErrorMessage(nameof(product)), UserErrorCode.PayloadNotProvided);
            }

            if (!productName.Equals(product.ProductName))
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposeNameMismatchErrorMessage(typeof(Product).Name),
                    UserErrorCode.NameMismatch);
            }

            if (!ControllerHelper.ValidateStringFormat(productName, ValidStringFormat.LOWER_CASE_NUMBER_AND_HYPHEN_50))
            {
                throw new LunaBadRequestUserException($"The product name is invalid. The naming rule: {ControllerHelper.GetStringFormatDescription(ValidStringFormat.LOWER_CASE_NUMBER_AND_HYPHEN_50)}", 
                    UserErrorCode.InvalidParameter);
            }

            if (await _productService.ExistsAsync(productName))
            {
                _logger.LogInformation($"Update product {productName} with payload {JsonConvert.SerializeObject(product)}");
                product = await _productService.UpdateAsync(productName, product);
                return Ok(product);
            }
            else
            {
                _logger.LogInformation($"Create product {productName} with payload {JsonConvert.SerializeObject(product)}");
                await _productService.CreateAsync(product);
                return CreatedAtRoute(nameof(GetAsync) + nameof(Product), new { productName = product.ProductName }, product);
            }
        }

        /// <summary>
        /// Deletes an product.
        /// </summary>
        /// <param name="productName">The name of the product to delete.</param>
        /// <returns>HTTP 204 NO CONTENT.</returns>
        [HttpDelete("products/{productName}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<ActionResult> DeleteAsync(string productName)
        {
            AADAuthHelper.VerifyUserAccess(this.HttpContext, _logger, true);
            _logger.LogInformation($"Delete product {productName}.");
            await _productService.DeleteAsync(productName);
            return NoContent();
        }
    }
}