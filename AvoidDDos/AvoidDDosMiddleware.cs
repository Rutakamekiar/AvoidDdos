using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.AspNetCore.Http;

namespace AvoidDDos
{
    public class AvoidDDosMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ConcurrentDictionary<IPAddress, int> _usersRequestsCount = new ConcurrentDictionary<IPAddress, int>();
        private readonly ConcurrentBag<IPAddress> _bannedIPAddresses = new ConcurrentBag<IPAddress>();

        public AvoidDDosMiddleware(RequestDelegate next)
        {
            _next = next;
            SetTimer();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var ip = context.Connection.RemoteIpAddress;
            if (_bannedIPAddresses.Contains(ip))
            {
                context.Response.ContentType = "text/plain";
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("too many requests");
                return;
            }

            CheckIpAddress(ip);

            await _next(context);
        }

        private void CheckIpAddress(IPAddress ip)
        {
            _usersRequestsCount.AddOrUpdate(ip,
                                            1,
                                            (ipAdr, reqCount) => reqCount + 1);

            if (_usersRequestsCount[ip] == 50)
            {
                Console.WriteLine($"ip:{ip} banned");
                _bannedIPAddresses.Add(ip);
                _usersRequestsCount.TryRemove(ip, out _);
            }
        }

        public void SetTimer()
        {
            var dictionaryTimer = new Timer();
            dictionaryTimer.Elapsed += CleanupDictionaryEverySecond;
            dictionaryTimer.Interval = 1000;
            dictionaryTimer.Enabled = true;

            var bannedListTimer = new Timer();
            bannedListTimer.Elapsed += CleanupBannedListEverySecond;
            bannedListTimer.Interval = 1000 * 60 * 60;
            bannedListTimer.Enabled = true;
        }

        private void CleanupDictionaryEverySecond(object source, ElapsedEventArgs e)
        {
            _usersRequestsCount.Clear();
        }

        private void CleanupBannedListEverySecond(object source, ElapsedEventArgs e)
        {
            _bannedIPAddresses.Clear();
        }
    }
}