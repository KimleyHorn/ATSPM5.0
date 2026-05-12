#region license
// Copyright 2025 Utah Departement of Transportation
// for Data - Utah.Udot.Atspm.Data.Utility/StringArrayValueComparer.cs
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion

using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Utah.Udot.Atspm.Data.Utility
{
    /// <summary>
    /// <see cref="ValueComparer"/> used to compare string arrays persisted via a value converter.
    /// </summary>
    internal class StringArrayValueComparer : ValueComparer<string[]>
    {
        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public StringArrayValueComparer() : base(
            (left, right) =>
                ReferenceEquals(left, right) ||
                (left != null && right != null && left.SequenceEqual(right)),
            value => value == null
                ? 0
                : value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item != null ? item.GetHashCode() : 0)),
            value => value == null ? Array.Empty<string>() : value.ToArray())
        { }
    }
}
