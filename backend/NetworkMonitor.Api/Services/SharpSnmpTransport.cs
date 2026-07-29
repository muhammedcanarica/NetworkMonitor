using System.Net;
using System.Net.Sockets;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public sealed class SharpSnmpTransport(ILogger<SharpSnmpTransport> logger) : ISnmpTransport
{
    private const int SnmpPort = 161;
    private const int BulkSize = 20;

    public async Task<IReadOnlyList<SnmpVariableValue>> GetAsync(
        SnmpConnection connection,
        IReadOnlyList<string> oids,
        CancellationToken cancellationToken)
    {
        var endpoint = CreateEndpoint(connection.IpAddress);
        var variables = oids
            .Select(oid => new Variable(new ObjectIdentifier(oid)))
            .ToList();
        var request = new GetRequestMessage(
            Messenger.RequestCounter.NextId,
            VersionCode.V2,
            new OctetString(connection.Community),
            variables);

        var response = await SendAsync(request, endpoint, connection, cancellationToken);
        return response.Pdu().Variables.Select(MapVariable).ToList();
    }

    public async Task<IReadOnlyList<SnmpVariableValue>> WalkAsync(
        SnmpConnection connection,
        string rootOid,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var endpoint = CreateEndpoint(connection.IpAddress);
        var community = new OctetString(connection.Community);
        var rootPrefix = rootOid + ".";
        var currentOid = new ObjectIdentifier(rootOid);
        var results = new List<SnmpVariableValue>(Math.Min(maxResults, 500));

        while (results.Count < maxResults)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var repetitions = Math.Min(BulkSize, maxResults - results.Count);
            var request = new GetBulkRequestMessage(
                Messenger.RequestCounter.NextId,
                VersionCode.V2,
                community,
                0,
                repetitions,
                [new Variable(currentOid)]);
            var response = await SendAsync(request, endpoint, connection, cancellationToken);
            var variables = response.Pdu().Variables;
            if (variables.Count == 0)
            {
                break;
            }

            ObjectIdentifier? lastAcceptedOid = null;
            foreach (var variable in variables)
            {
                if (variable.Data.TypeCode is SnmpType.EndOfMibView
                    or SnmpType.NoSuchInstance
                    or SnmpType.NoSuchObject)
                {
                    return results;
                }

                var oid = variable.Id.ToString();
                if (!oid.StartsWith(rootPrefix, StringComparison.Ordinal))
                {
                    return results;
                }

                if (variable.Id <= currentOid)
                {
                    throw new SnmpOperationException(
                        SnmpErrorKind.UnsupportedResponse,
                        "The SNMP agent returned a non-advancing WALK response.");
                }

                results.Add(MapVariable(variable));
                lastAcceptedOid = variable.Id;
                if (results.Count == maxResults)
                {
                    return results;
                }
            }

            if (lastAcceptedOid is null)
            {
                break;
            }

            currentOid = lastAcceptedOid;
        }

        return results;
    }

    private async Task<ISnmpMessage> SendAsync(
        ISnmpMessage request,
        IPEndPoint endpoint,
        SnmpConnection connection,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(connection.TimeoutMilliseconds);

        try
        {
            var response = await request.GetResponseAsync(endpoint, timeoutSource.Token);
            if (response.Pdu().ErrorStatus.ToInt32() != 0)
            {
                throw new SnmpOperationException(
                    SnmpErrorKind.UnsupportedResponse,
                    "The SNMP agent returned an error response.");
            }

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw new SnmpOperationException(
                SnmpErrorKind.Timeout,
                "The SNMP request timed out. The device may be unavailable or may not accept the supplied access configuration.",
                exception);
        }
        catch (SnmpOperationException)
        {
            throw;
        }
        catch (SocketException exception)
        {
            LogFailure(connection.IpAddress, exception);
            throw new SnmpOperationException(
                SnmpErrorKind.Unavailable,
                "The SNMP request could not reach the target device.",
                exception);
        }
        catch (SnmpException exception)
        {
            LogFailure(connection.IpAddress, exception);
            throw new SnmpOperationException(
                SnmpErrorKind.UnsupportedResponse,
                "The SNMP agent returned an unsupported response.",
                exception);
        }
        catch (Exception exception)
        {
            LogFailure(connection.IpAddress, exception);
            throw new SnmpOperationException(
                SnmpErrorKind.Unknown,
                "The SNMP request failed unexpectedly.",
                exception);
        }
    }

    private void LogFailure(string ipAddress, Exception exception)
    {
        logger.LogWarning(
            "SNMP request to {IpAddress} failed with {ErrorType}.",
            ipAddress,
            exception.GetType().Name);
    }

    private static IPEndPoint CreateEndpoint(string ipAddress)
    {
        return new IPEndPoint(IPAddress.Parse(ipAddress), SnmpPort);
    }

    private static SnmpVariableValue MapVariable(Variable variable)
    {
        var type = variable.Data.TypeCode.ToString();
        var value = variable.Data.TypeCode is SnmpType.NoSuchInstance
            or SnmpType.NoSuchObject
            or SnmpType.EndOfMibView
            or SnmpType.Null
            ? null
            : variable.Data.ToString();
        ulong? numericValue = variable.Data switch
        {
            Counter32 counter => counter.ToUInt32(),
            Counter64 counter => counter.ToUInt64(),
            Gauge32 gauge => gauge.ToUInt32(),
            TimeTicks ticks => ticks.ToUInt32(),
            Integer32 integer when integer.ToInt32() >= 0 => (ulong)integer.ToInt32(),
            _ => null
        };

        return new SnmpVariableValue(variable.Id.ToString(), value, type, numericValue);
    }
}
