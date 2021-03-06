using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Luna.Clients.Azure;
using Luna.Clients.Azure.Auth;
using Luna.Clients.Exceptions;
using Luna.Clients.Fulfillment;
using Luna.Clients.Logging;
using Luna.Data.DataContracts;
using Luna.Data.Entities;
using Luna.Data.Enums;
using Luna.Data.Repository;
using Luna.Services.Marketplace;
using Luna.Services.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Luna.Services.Data
{
    /// <summary>
    /// Service class that handles basic CRUD functionality for the subscription resource.
    /// </summary>
    public class SubscriptionService : ISubscriptionService
    {
        private readonly ISqlDbContext _context;
        private readonly IOfferService _offerService;
        private readonly IPlanService _planService;
        private readonly IOfferParameterService _offerParameterService;
        private readonly ICustomMeterService _customMeterService;
        private readonly ICustomMeterDimensionService _customMeterDimensionService;
        private readonly IFulfillmentManager _fulfillmentManager;
        private readonly IProductService _productService;
        private readonly IAIAgentService _aiAgentService;
        private readonly IDeploymentService _deploymentService;
        private readonly IAPISubscriptionService _apiSubscriptionService;
        private readonly ILogger<SubscriptionService> _logger;

        /// <summary>
        /// Constructor that uses dependency injection.
        /// </summary>
        /// <param name="sqlDbContext">The context to inject.</param>
        /// <param name="offerService">A service to inject.</param>
        /// <param name="planService">A service to inject.</param>
        /// <param name="customMeterDimensionService">A service to inject.</param>
        /// <param name="customMeterService">A service to inject.</param>
        /// <param name="offerParameterService">A service to inject.</param>
        /// <param name="logger">The logger.</param>
        public SubscriptionService(ISqlDbContext sqlDbContext,
            IOfferService offerService,
            IPlanService planService,
            IOfferParameterService offerParameterService,
            ICustomMeterDimensionService customMeterDimensionService,
            ICustomMeterService customMeterService,
            IFulfillmentManager fulfillmentManager,
            IProductService productService,
            IAIAgentService aiAgentService,
            IDeploymentService deploymentService,
            IAPISubscriptionService apiSubscriptionService,
            ILogger<SubscriptionService> logger)
        {
            _context = sqlDbContext ?? throw new ArgumentNullException(nameof(sqlDbContext));
            _offerService = offerService ?? throw new ArgumentNullException(nameof(offerService));
            _planService = planService ?? throw new ArgumentNullException(nameof(planService));
            _customMeterDimensionService = customMeterDimensionService ?? throw new ArgumentNullException(nameof(customMeterDimensionService));
            _customMeterService = customMeterService ?? throw new ArgumentNullException(nameof(customMeterService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _offerParameterService = offerParameterService ?? throw new ArgumentNullException(nameof(offerParameterService));
            _fulfillmentManager = fulfillmentManager ?? throw new ArgumentNullException(nameof(fulfillmentManager));
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _aiAgentService = aiAgentService ?? throw new ArgumentNullException(nameof(aiAgentService));
            _deploymentService = deploymentService ?? throw new ArgumentNullException(nameof(deploymentService));
            _apiSubscriptionService = apiSubscriptionService ?? throw new ArgumentNullException(nameof(apiSubscriptionService));
        }

        /// <summary>
        /// Gets all subscriptions.
        /// </summary>
        /// <param name="status">The list of status of the subscription.</param>
        /// <param name="owner">The owner of the subscription.</param>
        /// <returns>A list of all subsrciptions.</returns>
        public async Task<List<Subscription>> GetAllAsync(string[] status = null, string owner = "")
        {
            _logger.LogInformation(LoggingUtils.ComposeGetAllResourcesMessage(typeof(Subscription).Name));

            // Gets all subscriptions that have the provided status.
            List<Subscription> allSub = await _context.Subscriptions.ToListAsync();

            List<Subscription> subscriptionList = allSub.Where(s => (status == null || status.Contains(s.Status)) &&
                    (string.IsNullOrEmpty(owner) || s.Owner.Equals(owner, StringComparison.InvariantCultureIgnoreCase))).ToList();

            foreach (var sub in subscriptionList)
            {
                sub.PlanName = (await _context.Plans.FindAsync(sub.PlanId)).PlanName;
                sub.OfferName = (await _context.Offers.FindAsync(sub.OfferId)).OfferName;
                try
                {
                    var apiSubscription = await _apiSubscriptionService.GetAsync(sub.SubscriptionId);
                    sub.PrimaryKey = apiSubscription.PrimaryKey;
                    sub.SecondaryKey = apiSubscription.SecondaryKey;
                    sub.BaseUrl = apiSubscription.BaseUrl;
                }
                catch (LunaNotFoundUserException)
                {
                }
            }
            _logger.LogInformation(LoggingUtils.ComposeReturnCountMessage(typeof(Subscription).Name, subscriptionList.Count()));

            return subscriptionList;
        }


        /// <summary>
        /// Get all active subscription by offer name
        /// </summary>
        /// <param name="offerName">The offer name</param>
        /// <returns>The list of subscriptions</returns>
        public async Task<List<Subscription>> GetAllActiveByOfferName(string offerName)
        {
            var offer = await _offerService.GetAsync(offerName);
            //TODO: error handling

            List<Subscription> allSub = await _context.Subscriptions.ToListAsync();

            List<Subscription> subscriptionList = allSub.Where(s => s.OfferId == offer.Id).ToList();
            foreach (var sub in subscriptionList)
            {
                sub.PlanName = (await _context.Plans.FindAsync(sub.PlanId)).PlanName;
                sub.OfferName = offerName;
            }

            _logger.LogInformation(LoggingUtils.ComposeReturnCountMessage(typeof(Subscription).Name, subscriptionList.Count()));

            return subscriptionList;

        }

        /// <summary>
        /// Gets a subscription by id.
        /// </summary>
        /// <param name="subscriptionId">The id of the subscription.</param>
        /// <returns>The subscription.</returns>
        public async Task<Subscription> GetAsync(Guid subscriptionId)
        {
            _logger.LogInformation(LoggingUtils.ComposeGetSingleResourceMessage(typeof(Subscription).Name, subscriptionId.ToString()));

            // Find the subscription that matches the subscriptionId provided
            var subscription = await _context.Subscriptions.FindAsync(subscriptionId);

            // Check if subscription exists
            if (subscription is null)
            {
                throw new LunaNotFoundUserException(LoggingUtils.ComposeNotFoundErrorMessage(typeof(Subscription).Name,
                    subscriptionId.ToString()));
            }

            subscription.OfferName = (await _context.Offers.FindAsync(subscription.OfferId)).OfferName;
            subscription.PlanName = (await _context.Plans.FindAsync(subscription.PlanId)).PlanName;
            try
            {
                var apiSubscription = await _apiSubscriptionService.GetAsync(subscription.SubscriptionId);
                subscription.PrimaryKey = apiSubscription.PrimaryKey;
                subscription.SecondaryKey = apiSubscription.SecondaryKey;
                subscription.BaseUrl = apiSubscription.BaseUrl;
            }
            catch(LunaNotFoundUserException)
            {
            }
            _logger.LogInformation(LoggingUtils.ComposeReturnValueMessage(typeof(Subscription).Name,
                subscriptionId.ToString(),
                JsonSerializer.Serialize(subscription)));

            return subscription; //Task.FromResult(subscription);
        }

        /// <summary>
        /// Creates a subscription within a plan within an offer.
        /// </summary>
        /// <param name="subscription">The subscription to create.</param>
        /// <returns>The created subscription.</returns>
        public async Task<Subscription> CreateAsync(Subscription subscription)
        {
            if (subscription is null)
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposePayloadNotProvidedErrorMessage(typeof(Subscription).Name),
                    UserErrorCode.PayloadNotProvided);
            }

            if (await ExistsAsync(subscription.SubscriptionId))
            {
                throw new LunaConflictUserException(LoggingUtils.ComposeAlreadyExistsErrorMessage(typeof(Subscription).Name,
                    subscription.SubscriptionId.ToString()));
            }
            _logger.LogInformation(LoggingUtils.ComposeCreateResourceMessage(typeof(Subscription).Name, subscription.Name, offerName: subscription.OfferName, planName: subscription.PlanName, payload: JsonSerializer.Serialize(subscription)));

            var offerParameters = await _offerParameterService.GetAllAsync(subscription.OfferName);

            foreach (var param in offerParameters)
            {
                // treat string as list
                if (param.ValueType.Equals("list", StringComparison.InvariantCultureIgnoreCase))
                {
                    param.ValueType = "string";
                }
                // Check if value of all offer parameters are provided with correct type
                if (subscription.InputParameters.Where(x => x.Name.Equals(param.ParameterName) && x.Type.Equals(param.ValueType)).Count() < 1)
                {
                    throw new LunaBadRequestUserException($"Value of parameter {param.ParameterName} is not provided, or the type doesn't match.", UserErrorCode.ParameterNotProvided);
                }
            }

            // Get the offer associated with the offerName provided
            var offer = await _offerService.GetAsync(subscription.OfferName);


            // Get the plan associated with the planUniqueName provided
            var plan = await _planService.GetAsync(subscription.OfferName, subscription.PlanName);

            // Set the FK to offer
            subscription.OfferId = offer.Id;

            // Set the FK to plan
            subscription.PlanId = plan.Id;

            // Always set quantity to 1 to walkaround a marketplace service bug
            subscription.Quantity = 1;

            // Set the created time
            subscription.CreatedTime = DateTime.UtcNow;

            subscription.Status = nameof(FulfillmentState.PendingFulfillmentStart);

            subscription.ProvisioningStatus = nameof(ProvisioningState.ProvisioningPending);

            subscription.ProvisioningType = nameof(ProvisioningType.Subscribe);

            if (subscription.AgentId == null)
            {
                var agent = await _aiAgentService.GetSaaSAgentAsync();
                subscription.AgentId = agent.AgentId;
            }
            subscription.RetryCount = 0;

            List<CustomMeter> customMeterList = await _customMeterService.GetAllAsync(offer.OfferName);

            using (var transaction = await _context.BeginTransactionAsync())
            {
                // Add subscription to db
                _context.Subscriptions.Add(subscription);
                await _context._SaveChangesAsync();

                // Add subscription parameters
                foreach (var param in subscription.InputParameters)
                {
                    param.SubscriptionId = subscription.SubscriptionId;
                    _context.SubscriptionParameters.Add(param);
                }
                await _context._SaveChangesAsync();

                foreach (var meter in customMeterList)
                {
                    _context.SubscriptionCustomMeterUsages.Add(new SubscriptionCustomMeterUsage(meter.Id, subscription.SubscriptionId));
                }

                await _context._SaveChangesAsync();

                // Create API subscription if a product is linked or not
                /*
                var product = await _productService.GetByOfferNameAsync(offer.Id);
                if (product != null)
                {
                    if (await _apiSubscriptionService.ExistsAsync(subscription.SubscriptionId))
                    {
                        await _apiSubscriptionService.UpdateAsync(subscription.SubscriptionId, new APISubscription(subscription));
                    }
                    else
                    {
                        await _apiSubscriptionService.CreateAsync(new APISubscription(subscription));
                    }
                }
                */

                transaction.Commit();
            }
            _logger.LogInformation(LoggingUtils.ComposeResourceCreatedMessage(typeof(Subscription).Name, subscription.Name, offerName: subscription.OfferName, planName: subscription.PlanName));

            return subscription;
        }

        public void CheckSubscriptionInReadyState(Subscription subscription)
        {
            if (!subscription.Status.Equals(nameof(FulfillmentState.Subscribed)))
            {
                throw new NotSupportedException(LoggingUtils.ComposeSubscriptionActionErrorMessage(subscription.SubscriptionId, SubscriptionAction.Update.ToVerb(), invalidFulfillmentState: nameof(subscription.Status)));
            }

            if (!subscription.ProvisioningStatus.Equals(nameof(ProvisioningState.Succeeded)))
            {
                throw new NotSupportedException(LoggingUtils.ComposeSubscriptionActionErrorMessage(subscription.SubscriptionId, SubscriptionAction.Update.ToVerb(), invalidProvisioningState: true));
            }

            _logger.LogInformation($"Subscription {subscription.SubscriptionId} is in the Subscribed fulfillment state and Succeeded provisioining state.");
        }

        /// <summary>
        /// Updates a subscription.
        /// </summary>
        /// <param name="subscription">The updated subscription.</param>
        /// <returns>The updated subscription.</returns>
        public async Task<Subscription> UpdateAsync(Subscription subscription, Guid operationId)
        {
            if (subscription is null)
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposePayloadNotProvidedErrorMessage(typeof(Subscription).Name),
                    UserErrorCode.PayloadNotProvided);
            }
            _logger.LogInformation(LoggingUtils.ComposeUpdateResourceMessage(typeof(Subscription).Name, subscription.Name, offerName: subscription.OfferName, planName: subscription.PlanName, payload: JsonSerializer.Serialize(subscription)));
            var newPlanName = subscription.PlanName;
            var newQuantity = subscription.Quantity;

            Subscription subscriptionDb;
            try
            {
                // Get the subscription that matches the subscriptionId provided
                subscriptionDb = await GetAsync(subscription.SubscriptionId);
            }
            catch (Exception)
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposeSubscriptionActionErrorMessage(subscription.SubscriptionId, SubscriptionAction.Update.ToVerb()), UserErrorCode.ResourceNotFound);
            }

            CheckSubscriptionInReadyState(subscriptionDb);

            // Get the offer and plan associated with the subscriptionId provided
            var offer = await _context.Offers.FindAsync(subscriptionDb.OfferId);
            var plan = await _context.Plans.FindAsync(subscriptionDb.PlanId);

            if (newPlanName != plan.PlanName && subscriptionDb.Quantity != newQuantity)
            {
                throw new ArgumentException("Cannot update plan and quantity at the same time.");
            }

            // Check if the plan has been upgraded or downgraded
            if (newPlanName != plan.PlanName)
            {
                _logger.LogInformation($"Updating subscription {subscription.SubscriptionId} from plan {plan.PlanName} to {newPlanName}.");
                // Get the new plan to change to 
                var newPlan = await _planService.GetAsync(offer.OfferName, newPlanName);

                // Update the FK to the new plan
                subscription.OperationId = operationId;
                subscriptionDb.PlanId = newPlan.Id;
                subscriptionDb.ProvisioningStatus = nameof(ProvisioningState.ArmTemplatePending);
                subscriptionDb.ProvisioningType = nameof(ProvisioningType.Update);
            }
            else if (subscriptionDb.Quantity != subscription.Quantity)
            {
                _logger.LogInformation($"Updating subscription {subscription.SubscriptionId} from quantity {subscriptionDb.Quantity} to {subscription.Quantity}");
                subscriptionDb.Quantity = newQuantity;
                //TODO: what to do?
            }

            // Set the updated time
            subscriptionDb.LastUpdatedTime = DateTime.UtcNow;

            // Update subscriptionDb values and save changes in db
            _context.Subscriptions.Update(subscriptionDb);
            await _context._SaveChangesAsync();
            _logger.LogInformation(LoggingUtils.ComposeResourceUpdatedMessage(typeof(Subscription).Name, subscription.Name, offerName: subscription.OfferName, planName: subscription.PlanName));

            return subscriptionDb;
        }

        /// <summary>
        /// Soft delete a subscription.
        /// </summary>
        /// <param name="subscriptionId">The id of the subscription to soft delete.</param>
        /// <returns>The subscription with updated status and unsubscribed_time.</returns>
        public async Task<Subscription> UnsubscribeAsync(Guid subscriptionId, Guid operationId)
        {
            Subscription subscription;
            try
            {
                // Get the subscription that matches the subscriptionId provided
                subscription = await GetAsync(subscriptionId);
            }
            catch (Exception)
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposeSubscriptionActionErrorMessage(subscriptionId, SubscriptionAction.Unsubscribe.ToVerb()), UserErrorCode.ResourceNotFound);
            }

            CheckSubscriptionInReadyState(subscription);
            _logger.LogInformation($"Operation {operationId}: Unsubscribe subscription {subscriptionId} with offer {subscription.OfferName} and plan {subscription.PlanName}.");
            // Soft delete the subscription from db
            subscription.OperationId = operationId;
            subscription.ProvisioningStatus = nameof(ProvisioningState.ArmTemplatePending);
            subscription.ProvisioningType = nameof(ProvisioningType.Unsubscribe);
            subscription.LastUpdatedTime = DateTime.UtcNow;

            using (var transaction = await _context.BeginTransactionAsync())
            {
                var subscriptionMeterUsages = await _context.SubscriptionCustomMeterUsages.Where(s => s.IsEnabled && s.SubscriptionId == subscriptionId).ToListAsync();

                foreach(var usage in subscriptionMeterUsages)
                {
                    usage.UnsubscribedTime = subscription.LastUpdatedTime.Value;
                    _context.SubscriptionCustomMeterUsages.Update(usage);
                }

                await _context._SaveChangesAsync();

                _context.Subscriptions.Update(subscription);
                await _context._SaveChangesAsync();

                transaction.Commit();
            }
            _logger.LogInformation($"Operation {operationId}: Subscription {subscriptionId} with offer {subscription.OfferName} and plan {subscription.PlanName} is unsubscribed.");

            return subscription;
        }

        /// <summary>
        /// Suspend the subscription
        /// </summary>
        /// <param name="subscriptionId"></param>
        /// <returns></returns>
        public async Task<Subscription> SuspendAsync(Guid subscriptionId, Guid operationId)
        {
            Subscription subscription;
            try
            {
                // Get the subscription that matches the subscriptionId provided
                subscription = await GetAsync(subscriptionId);
            }
            catch (Exception)
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposeSubscriptionActionErrorMessage(subscriptionId, SubscriptionAction.Suspend.ToVerb()), UserErrorCode.ResourceNotFound);
            }

            CheckSubscriptionInReadyState(subscription);
            _logger.LogInformation($"Operation {operationId}: Suspend subscription {subscriptionId} with offer {subscription.OfferName} and plan {subscription.PlanName}.");

            // Soft delete the subscription from db
            subscription.OperationId = operationId;
            subscription.ProvisioningStatus = nameof(ProvisioningState.ArmTemplatePending);
            subscription.ProvisioningType = nameof(ProvisioningType.Suspend);
            subscription.LastUpdatedTime = DateTime.UtcNow;

            _context.Subscriptions.Update(subscription);
            await _context._SaveChangesAsync();
            _logger.LogInformation($"Operation {operationId}: Subscription {subscriptionId} with offer {subscription.OfferName} and plan {subscription.PlanName} is suspended.");

            return subscription;
        }

        /// <summary>
        /// Reinstate the subscription
        /// </summary>
        /// <param name="subscriptionId"></param>
        /// <returns></returns>
        public async Task<Subscription> ReinstateAsync(Guid subscriptionId, Guid operationId)
        {
            Subscription subscription;
            try
            {
                // Get the subscription that matches the subscriptionId provided
                subscription = await GetAsync(subscriptionId);
            }
            catch (Exception)
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposeSubscriptionActionErrorMessage(subscriptionId, SubscriptionAction.Reinstate.ToVerb()), UserErrorCode.ResourceNotFound);
            }

            if (!subscription.ProvisioningStatus.Equals(nameof(ProvisioningState.Succeeded)))
            {
                throw new NotSupportedException(LoggingUtils.ComposeSubscriptionActionErrorMessage(subscriptionId, SubscriptionAction.Reinstate.ToVerb(), invalidProvisioningState: true));
            }

            if (!subscription.Status.Equals(nameof(FulfillmentState.Suspended)))
            {
                throw new NotSupportedException(LoggingUtils.ComposeSubscriptionActionErrorMessage(subscriptionId, SubscriptionAction.Reinstate.ToVerb(), requiredFulfillmentState: nameof(FulfillmentState.Suspended)));
            }
            _logger.LogInformation($"Operation {operationId}: Reinstate subscription {subscriptionId} with offer {subscription.OfferName} and plan {subscription.PlanName}.");

            // Soft delete the subscription from db
            subscription.OperationId = operationId;
            subscription.ProvisioningStatus = nameof(ProvisioningState.ArmTemplatePending);
            subscription.ProvisioningType = nameof(ProvisioningType.Reinstate);
            subscription.LastUpdatedTime = DateTime.UtcNow;

            _context.Subscriptions.Update(subscription);
            await _context._SaveChangesAsync();
            _logger.LogInformation($"Operation {operationId}: Reinstate {subscriptionId} with offer {subscription.OfferName} and plan {subscription.PlanName} is suspended.");

            return subscription;
        }

        /// <summary>
        /// Delete data from a subscription
        /// </summary>
        /// <param name="subscriptionId">the subscription id</param>
        /// <returns>Purged subscription</returns>
        public async Task<Subscription> DeleteDataAsync(Guid subscriptionId)
        {
            Subscription subscription;
            try
            {
                // Get the subscription that matches the subscriptionId provided
                subscription = await GetAsync(subscriptionId);
            }
            catch (Exception)
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposeSubscriptionActionErrorMessage(subscriptionId, SubscriptionAction.DeleteData.ToVerb()), UserErrorCode.ResourceNotFound);
            }

            if (subscription == null)
            {
                throw new NotSupportedException($"Subscription {subscriptionId} doesn't exist or you don't have permission to access it.");
            }

            if (!subscription.ProvisioningStatus.Equals(nameof(ProvisioningState.Succeeded)))
            {
                throw new NotSupportedException(LoggingUtils.ComposeSubscriptionActionErrorMessage(subscriptionId, SubscriptionAction.DeleteData.ToVerb(), invalidProvisioningState: true));
            }

            if (!subscription.Status.Equals(nameof(FulfillmentState.Unsubscribed)))
            {
                throw new NotSupportedException(LoggingUtils.ComposeSubscriptionActionErrorMessage(subscriptionId, SubscriptionAction.DeleteData.ToVerb(), requiredFulfillmentState: nameof(FulfillmentState.Unsubscribed)));
            }

            _logger.LogInformation($"Delete data for subscription {subscriptionId}.");
            subscription.ProvisioningStatus = nameof(ProvisioningState.ArmTemplatePending);
            subscription.ProvisioningType = nameof(ProvisioningType.DeleteData);
            subscription.LastUpdatedTime = DateTime.UtcNow;

            _context.Subscriptions.Update(subscription);
            await _context._SaveChangesAsync();
            _logger.LogInformation($"Data deleted for subscription {subscriptionId}.");
            return subscription;
        }

        /// <summary>
        /// Activate a subscription.
        /// </summary>
        /// <param name="subscriptionId">The id of the subscription to activate.</param>
        /// <param name="activatedBy">The id of the user who activated this subscription.</param>
        /// <returns>The activated subscription.</returns>
        public async Task<Subscription> ActivateAsync(Guid subscriptionId, string activatedBy = "system")
        {
            Subscription subscription;
            try
            {
                // Get the subscription that matches the subscriptionId provided
                subscription = await GetAsync(subscriptionId);
            }
            catch (Exception)
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposeSubscriptionActionErrorMessage(subscriptionId, SubscriptionAction.Activate.ToVerb()), UserErrorCode.ResourceNotFound);
            }

            _logger.LogInformation($"Activate subscription {subscriptionId}.");
            subscription.Status = nameof(FulfillmentState.Subscribed);
            subscription.ActivatedTime = DateTime.UtcNow;
            subscription.ActivatedBy = activatedBy;

            _context.Subscriptions.Update(subscription);
            await _context._SaveChangesAsync();
            _logger.LogInformation($"Activated subscription {subscriptionId}. Activated by: {activatedBy}.");
            return subscription;
        }

        /// <summary>
        /// Checks if a subscription exists.
        /// </summary>
        /// <param name="subscriptionId">The id of the subscription to check exists.</param>
        /// <returns>True if exists, false otherwise.</returns>
        public async Task<bool> ExistsAsync(Guid subscriptionId)
        {
            _logger.LogInformation(LoggingUtils.ComposeCheckResourceExistsMessage(typeof(Subscription).Name, subscriptionId.ToString()));
            // Check that only one subscription with this subscriptionId exists 
            var count = await _context.Subscriptions
                .CountAsync(s => s.SubscriptionId == subscriptionId);

            // More than one instance of an object with the same name exists, this should not happen
            if (count > 1)
            {
                throw new NotSupportedException(LoggingUtils.ComposeFoundDuplicatesErrorMessage(typeof(Subscription).Name, subscriptionId.ToString()));

            }
            else if (count == 0)
            {
                _logger.LogInformation(LoggingUtils.ComposeResourceExistsOrNotMessage(typeof(Subscription).Name, subscriptionId.ToString(), false));
                return false;
            }
            else
            {
                _logger.LogInformation(LoggingUtils.ComposeResourceExistsOrNotMessage(typeof(Subscription).Name, subscriptionId.ToString(), true));
                // count = 1
                return true;
            }
        }

        /// <summary>
        /// Get warnings from subscription
        /// </summary>
        /// <param name="subscriptionId">Subscription id. Get all warnings if not specified</param>
        /// <returns>warnings</returns>
        public async Task<List<SubscriptionWarning>> GetWarnings(Guid? subscriptionId = null)
        {
            var subList = _context.Subscriptions.ToList().Where(s => ProvisioningHelper.IsErrorOrWarningProvisioningState(s.ProvisioningStatus) &&
                (subscriptionId == null || s.SubscriptionId == subscriptionId)).ToList();

            List<SubscriptionWarning> warnings = new List<SubscriptionWarning>();
            foreach (var sub in subList)
            {
                warnings.Add(new SubscriptionWarning(sub.SubscriptionId,
                    string.Format("Subscription in error state {0} since {1}.", sub.ProvisioningStatus, sub.LastUpdatedTime),
                    string.Format("Last exception: {0}.", sub.LastException)));
            }

            return warnings;
        }

        /// <summary>
        /// Get the subscription layout for landing page from token
        /// </summary>
        /// <param name="token">The token</param>
        /// <param name="userName">The current user name</param>
        /// <returns></returns>
        public async Task<SubscriptionLayout> GetSubscriptionLayoutFromToken(string token, string userName)
        {
            if (token.Split('.').Length != 3)
            {
                if (token.Equals("foo"))
                {
                    var offerParameters = await _offerParameterService.GetAllAsync("test1");
                    return new SubscriptionLayout(Guid.NewGuid(), "mysub", new OfferLayout("test1", "test 1"),
                        new List<PlanLayout>(new PlanLayout[] { new PlanLayout("test", "Test Plan") }),
                        new List<string>(new string[] { "SaaS" }),
                        offerParameters);
                }
                else
                {
                    //This is a marketplace token
                    MarketplaceSubscription resolvedSubscription = await _fulfillmentManager.ResolveSubscriptionAsync(token);
                    Offer offer = await _offerService.GetAsync(resolvedSubscription.OfferId);
                    Plan plan = await _planService.GetAsync(resolvedSubscription.OfferId, resolvedSubscription.PlanId);
                    var offerParameters = await _offerParameterService.GetAllAsync(resolvedSubscription.OfferId);
                    Product product = await _productService.GetByOfferNameAsync(offer.Id);
                    List<string> hostTypes = new List<string>();
                    if (product == null)
                    {
                        hostTypes.Add("SaaS");
                    }
                    else
                    {
                        hostTypes = GetHostTypes(product.HostType);
                    }

                    return new SubscriptionLayout(resolvedSubscription.SubscriptionId, resolvedSubscription.SubscriptionName,
                        new OfferLayout(offer.OfferName, offer.OfferName),
                        new List<PlanLayout>(new PlanLayout[] { new PlanLayout(plan.PlanName, plan.PlanName) }),
                        hostTypes,
                        offerParameters);
                }
            }
            else
            {
                var jwt_token = new JwtSecurityToken(token);
                string agentId = jwt_token.Header["aid"].ToString();

                var aiAgent = await _aiAgentService.GetAsync(new Guid(agentId));

                var handler = new JwtSecurityTokenHandler();
                var param = new TokenValidationParameters();
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(aiAgent.AgentKey));
                param.IssuerSigningKey = key;
                param.ValidateAudience = false;
                param.ValidIssuer = agentId;
                SecurityToken decodedToken;
                handler.ValidateToken(token, param, out decodedToken);
                string prodName = "";
                string agentUrl = "";
                foreach (var claim in ((JwtSecurityToken)decodedToken).Claims)
                {
                    if (claim.Type.Equals("uid"))
                    {
                        if (!AADAuthHelper.VerifyUserFromJwtToken(userName, claim.Value, _logger))
                        {
                            throw new LunaBadRequestUserException("The uid in JWT token is invalid.", UserErrorCode.InvalidToken);
                        }
                    }
                    if (claim.Type.Equals("prod"))
                    {
                        prodName = claim.Value;
                    }
                    if (claim.Type.Equals("url"))
                    {
                        agentUrl = claim.Value;
                    }
                }

                if (string.IsNullOrEmpty(prodName))
                {
                    throw new LunaBadRequestUserException("The prod in JWT token is invalid.", UserErrorCode.InvalidToken);
                }
                    
                if (string.IsNullOrEmpty(agentUrl))
                {
                    throw new LunaBadRequestUserException("The url in JWT token is invalid.", UserErrorCode.InvalidToken);
                }

                Product product = await _productService.GetAsync(prodName);
                List<Deployment> deploymentList = await _deploymentService.GetAllAsync(prodName);
                List<PlanLayout> plans = new List<PlanLayout>();
                foreach (var dep in deploymentList)
                {
                    plans.Add(new PlanLayout(dep.DeploymentName, dep.DeploymentName));
                }
                var hostTypes = GetHostTypes(product.HostType);

                return new SubscriptionLayout(Guid.NewGuid(), "",
                    new OfferLayout(product.ProductName, product.ProductName),
                    plans,
                    hostTypes,
                    agentUrl: agentUrl);

            }

        }
        private List<string> GetHostTypes(string hostTypeTag)
        {
            List<string> hostTypes = new List<string>();
            if (hostTypeTag.Equals("SaaS", StringComparison.InvariantCultureIgnoreCase))
            {
                hostTypes.Add("SaaS");
            }
            else if (hostTypeTag.Equals("BYOC", StringComparison.InvariantCultureIgnoreCase))
            {
                hostTypes.Add("Selfhost");
            }
            else if (hostTypeTag.Equals("Both", StringComparison.InvariantCultureIgnoreCase))
            {
                hostTypes.Add("Selfhost");
                hostTypes.Add("SaaS");
            }
            return hostTypes;
        }
    }
}