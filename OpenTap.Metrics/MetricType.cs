//            Copyright Keysight Technologies 2012-2024
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;

namespace OpenTap.Metrics;

[Flags]
public enum MetricType
{
    Unknown = 0,
    Double = 1,
    Boolean = 2,
    String = 4,
    Nullable = 8
}
