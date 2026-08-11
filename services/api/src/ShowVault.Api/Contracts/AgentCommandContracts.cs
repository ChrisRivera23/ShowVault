using ShowVault.AgentContracts;

namespace ShowVault.Api.Contracts;

public sealed record IssueAgentCommandRequest(
    AgentCommandType Type,
    string Payload,
    int ValidForSeconds = 300);
