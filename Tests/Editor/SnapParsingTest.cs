using System;
using System.Globalization;
using System.Reflection;
using NUnit.Framework;

namespace Csound.Unity.Tests
{
    public class SnapParsingTest
    {
        private static readonly MethodInfo ParseChannel = typeof(CsoundUnity).GetMethod(
            "ParseChannel", BindingFlags.NonPublic | BindingFlags.Static);

        /// <summary>Snap numbers retain their decimal point and exponent in every supported locale.</summary>
        [TestCase("en-US")]
        [TestCase("it-IT")]
        [TestCase("de-DE")]
        public void Numeric_values_are_independent_of_current_culture(string culture)
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                foreach (var value in new[] { 0.5f, -12.25f, 0.001f })
                {
                    var text = value.ToString("E", CultureInfo.InvariantCulture);
                    var channel = (CsoundChannelController)ParseChannel.Invoke(null, new object[] { "\"gain\":" + text });
                    Assert.AreEqual("gain", channel.channel);
                    Assert.AreEqual(value, channel.value, 1e-6f);
                }
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        /// <summary>Malformed and non-finite values are rejected instead of silently importing zero.</summary>
        [TestCase("\"gain\":invalid")]
        [TestCase("\"gain\":")]
        [TestCase("\"gain\":NaN")]
        [TestCase("\"gain\":Infinity")]
        [TestCase("\"gain\":1e100")]
        [TestCase("\"gain\"")]
        public void Invalid_entries_report_a_format_error(string entry)
        {
            var error = Assert.Throws<TargetInvocationException>(() => ParseChannel.Invoke(null, new object[] { entry }));
            Assert.IsInstanceOf<FormatException>(error.InnerException);
        }
    }
}
