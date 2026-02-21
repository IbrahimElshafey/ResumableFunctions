using ClientOnboarding.InOuts;
using ClientOnboarding.Services;
using ClientOnboarding.Workflow;
using Microsoft.Extensions.DependencyInjection;
using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.Testing;

namespace Tests
{
    public partial class ClientOnboardingTest
    {
        [Fact]
        public async Task ClientOnBoarding_NoSimulate_Test()
        {
            using var test = new TestShell(
                nameof(ClientOnBoarding_NoSimulate_Test),
                typeof(ClientOnboardingService),
                typeof(ClientOnboardingWorkflowPublic));
            test.RegisteredServices.AddScoped<IClientOnboardingService, ClientOnboardingService>();
            await test.ScanTypes();

            var service = test.CurrentApp.Services.GetService<IClientOnboardingService>();
            var registration = service.ClientFillsForm(new RegistrationForm { UserId = 2000, FormData = "Form Data" });
            var currentInstance = await RoundCheck(test, 1);

            var ownerApprove = service.OwnerApproveClient(new OwnerApproveClientInput { Decision = true, TaskId = currentInstance.OwnerTaskId });
            currentInstance = await RoundCheck(test, 2);


            var meetingResult = service.SendMeetingResult(currentInstance.ClientMeetingId);
            currentInstance = await RoundCheck(test, 3, true);
        }

        private async Task<ClientOnboardingWorkflowPublic> RoundCheck(TestShell test, int round, bool finished = false)
        {
            var pushedCalls = await test.GetPushedCalls();
            Assert.Equal(round, pushedCalls.Count);
            var waits = await test.GetWaits();
            Assert.Equal(finished ? round : round + 1, waits.Count);
            var instances = await test.GetInstances<ClientOnboardingWorkflowPublic>();
            Assert.Single(instances);
            if (finished)
                Assert.Equal(FunctionInstanceStatus.Completed, instances[0].Status);
            return instances[0].StateObject as ClientOnboardingWorkflowPublic;
        }
    }
}