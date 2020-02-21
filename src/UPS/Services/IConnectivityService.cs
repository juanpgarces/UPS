using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace UPS.Services
{
    public interface IConnectivityService
    {
        Task<bool> HasConnectivity();
        Task<bool> HasConnectivityTo(Uri uri);
        Task<bool> HasConnectivityTo(string uri);

    }
}
