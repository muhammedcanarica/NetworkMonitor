namespace NetworkMonitor.Api.Services;

public interface ISecretProtector
{
    string Protect(string secret);
    string Unprotect(string protectedSecret);
}
