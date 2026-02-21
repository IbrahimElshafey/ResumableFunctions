using ResumableFunctions.Handler.Core.Abstraction;

namespace ResumableFunctions.Handler.Core
{
    internal class LockNames : ILockNames
    {
        private readonly IResumableFunctionsSettings _settings;

        public LockNames(IResumableFunctionsSettings settings)
        {
            _settings = settings;
        }
        public string UpdateFunctionState(int functionState) => $"{_settings.CurrentWaitsDbName}_{nameof(UpdateFunctionState)}_{functionState}";
    }
}
