using LeoQ.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace LeoQ.Core.Abstractions
{
    public interface ICryptoOverheadModel
    {
        string Name { get; }
        double SampleCryptoOverheadMs(ScenarioConfig scenario);
    }
}
