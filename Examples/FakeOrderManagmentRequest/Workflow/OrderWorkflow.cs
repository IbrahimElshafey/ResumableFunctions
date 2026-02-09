using FakeOrderManagmentRequest.Services;
using Hangfire.Server;
using ResumableFunctions.Handler;
using ResumableFunctions.Handler.Attributes;
using ResumableFunctions.Handler.BaseUse;

namespace FakeOrderManagmentRequest.Workflow
{
    public class OrderWorkflow : ResumableFunctionsContainer
    {
        IOrderProcessingService _service;
        public void SetDependencies(IOrderProcessingService service)
        {
            _service = service;
        }
        //Client create order
        //System send task to operation team to review the order
        //Operation team will approve or reject the task
        //if operation team 
        // approved the task >> system send notification to client of success
        // reject the task >>
        // system send notification to client of rejection
        // Refund process will start
        // Task to finance team to refund the order
        // notify client that order refunded successfly
        public int OrderId { get; set; }
        public int ClientId { get; set; }
        public async Task<int> Test()
        {
            int x = 1;
            await Task.Delay(1000);
            x = 2;
            await Task.Delay(1000);
            x = 3;
            return x;
        }

        [ResumableFunctionEntryPoint("OrderWorkflow")]
        public async IAsyncEnumerable<Wait> StartWorkflow()
        {
            yield return
                WaitMethod<Order, OrderCreationResult>(_service.OrderCreated)
                .AfterMatch((order, result) =>
                {
                    OrderId = result.OrderId;
                    ClientId = order.ClientId;
                });

            var taskId = await _service.ReviewOrderTask(OrderId);
            OrderReviewTask reviewTask = null;
            yield return
               WaitMethod<OrderReviewTask, OrderReviewTaskResult>(_service.FinanceTeamReviewTask)
               .MatchIf((orderReviewTask, result) => orderReviewTask.TaskId == taskId)
               .AfterMatch((orderReviewTask, result) =>
               {
                   reviewTask = orderReviewTask;
                   //throw new Exception("Something wrong happened.");
               });

            if (reviewTask.IsApproved)
                _service.SendNotification(ClientId, $"Order {OrderId} Approved");
            else if (reviewTask.IsRejected)
            {
                _service.SendNotification(ClientId, $"Order {OrderId} Rejected");
                var refundTaskId = _service.CreateRefundTask(OrderId);
                yield return
                  WaitMethod<RefundTask, RefundTaskResult>(_service.FinanceTeamRefundOrder)
                  .MatchIf((refundTask, result) => refundTask.TaskId == refundTaskId);
                _service.SendNotification(ClientId, $"Refunf for order {OrderId} done.");
            }
        }
    }
}
