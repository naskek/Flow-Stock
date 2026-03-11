using FlowStock.Server.Tests.CreateOrder.Infrastructure;

namespace FlowStock.Server.Tests.IncomingRequestsOrderConvergence.Infrastructure;

[CollectionDefinition("IncomingRequestsOrderConvergence", DisableParallelization = true)]
public sealed class IncomingRequestsOrderConvergenceCollectionDefinition : ICollectionFixture<CreateOrderPartnerStatusesFixture>
{
}
