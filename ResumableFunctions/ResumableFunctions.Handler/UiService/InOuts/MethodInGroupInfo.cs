using ResumableFunctions.Handler.InOuts.Entities;

namespace ResumableFunctions.Handler.UiService.InOuts;

public record MethodInGroupInfo(string ServiceName, WaitMethodIdentifier Method, string GroupUrn);