//            Copyright Keysight Technologies 2012-2024
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;

namespace OpenTap.Metrics;

[Flags]
public enum MetricKind
{
    /// <summary> This metric can be polled. </summary>
    [Display("Poll", "This metric can be polled.")]
    Poll = 1,
    /// <summary> This metric can be pushed out of band. </summary>
    [Display("Push", "This metric can be pushed.")]
    Push = 2,
    /// <summary> This metric can be polled and pushed out of band. </summary>
    [Display("Push & Poll", "This metric can be pushed and polled.")]PushPoll = Push | Poll,
}
