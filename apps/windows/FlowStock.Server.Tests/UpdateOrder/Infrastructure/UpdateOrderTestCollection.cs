using FlowStock.Server.Tests.CreateOrder.Infrastructure;

namespace FlowStock.Server.Tests.UpdateOrder.Infrastructure;

[CollectionDefinition("UpdateOrder", DisableParallelization = true)]
public sealed class UpdateOrderCollectionDefinition : ICollectionFixture<CreateOrderPartnerStatusesFixture>
{
}
