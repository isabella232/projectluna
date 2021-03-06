﻿using System.Threading.Tasks;
using Luna.Data.Entities;
using Luna.Data.Entities;
using Luna.Data.Entities.Luna.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Luna.Data.Repository
{
    public interface ISqlDbContext
    {
        DbSet<AadSecretTmp> AadSecretTmps { get; set; }
        DbSet<ArmTemplateParameter> ArmTemplateParameters { get; set; }
        DbSet<ArmTemplate> ArmTemplates { get; set; }
        DbSet<CustomMeterDimension> CustomMeterDimensions { get; set; }
        DbSet<CustomMeter> CustomMeters { get; set; }
        DbSet<IpAddress> IpAddresses { get; set; }
        DbSet<IpBlock> IpBlocks { get; set; }
        DbSet<IpConfig> IpConfigs { get; set; } 
        DbSet<OfferParameter> OfferParameters { get; set; }
        DbSet<Offer> Offers { get; set; }
        DbSet<Plan> Plans { get; set; }
        DbSet<RestrictedUser> RestrictedUsers { get; set; }
        DbSet<Subscription> Subscriptions { get; set; }
        DbSet<Webhook> Webhooks { get; set; }
        DbSet<WebhookParameter> WebhookParameters { get; set; }
        DbSet<SubscriptionParameter> SubscriptionParameters { get; set; }
        DbSet<ArmTemplateArmTemplateParameter> ArmTemplateArmTemplateParameters { get; set; }
        DbSet<WebhookWebhookParameter> WebhookWebhookParameters { get; set; }

        DbSet<SubscriptionCustomMeterUsage> SubscriptionCustomMeterUsages { get; set; }

        DbSet<TelemetryDataConnector> TelemetryDataConnectors { get; set; }

        DbSet<Product> Products { get; set; }
        DbSet<Deployment> Deployments { get; set; }
        DbSet<APIVersion> APIVersions { get; set; }
        DbSet<AMLWorkspace> AMLWorkspaces { get; set; }

        DbSet<APISubscription> APISubscriptions { get; set; }

        DbSet<AIAgent> AIAgents { get; set; }

        DbSet<AgentSubscription> AgentSubscriptions { get; set; }

        DbSet<AgentAPIVersion> AgentAPIVersions { get; set; }

        DbSet<Publisher> Publishers { get; set; }

        DbSet<AgentOffer> AgentOffers { get; set; }

        // Wrappers for DbContext methods that are used
        Task<int> _SaveChangesAsync();
        Task<IDbContextTransaction> BeginTransactionAsync();
    }
}