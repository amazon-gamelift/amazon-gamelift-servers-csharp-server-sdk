/*
* All or portions of this file Copyright (c) Amazon.com, Inc. or its affiliates or
* its licensors.
*
* For complete copyright and license terms please see the LICENSE at the root of this
* distribution (the "License"). All use of this software is governed by the License,
* or, if provided, by the license below or the license accompanying this file. Do not
* remove or modify any license notices. This file is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
*
*/

namespace Aws.GameLift.Server.Model.Metrics
{
    using System;

    /// <summary>
    /// Represents a validated tag for metrics.
    /// Tags must adhere to the following naming conventions:
    /// - Must start with a letter.
    /// - Can contain alphanumerics, underscores, minuses, colons, periods, and slashes.
    /// - Cannot exceed 200 characters.
    /// - Must contain at least one colon (:) to delimit the tag key and value.
    /// - Are case-sensitive.
    /// </summary>
    public class Tag
    {
        private const int MaxTagLength = 200;

        /// <summary>
        /// Initializes a new instance of the <see cref="Tag"/> class.
        /// </summary>
        /// <param name="value">The tag value.</param>
        /// <exception cref="ArgumentException">Thrown when the tag value is invalid.</exception>
        public Tag(string value)
        {
            ValidateTag(value);
            int colonIndex = value.IndexOf(':');
            Key = value.Substring(0, colonIndex);
            Value = value.Substring(colonIndex + 1);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Tag"/> class.
        /// </summary>
        /// <param name="key">The tag key.</param>
        /// <param name="value">The tag value.</param>
        /// <exception cref="ArgumentException">Thrown when the tag value is invalid.</exception>
        public Tag(string key, string value)
        {
            ValidateTagNotNullOrEmpty(key);
            ValidateTagNotNullOrEmpty(value);
            ValidateTag(key + ":" + value);

            Key = key;
            Value = value;
        }

        /// <summary>
        /// Gets the key of the tag.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets the string value of the tag.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Implicit conversion from string to Tag.
        /// </summary>
        /// <param name="value">The string value to convert.</param>
        /// <returns>A new Tag instance.</returns>
        public static implicit operator Tag(string value)
        {
            return new Tag(value);
        }

        /// <summary>
        /// Implicit conversion from Tag to string.
        /// </summary>
        /// <param name="tag">The Tag to convert.</param>
        /// <returns>The string value of the tag.</returns>
        public static implicit operator string(Tag tag)
        {
            return tag?.ToString();
        }

        /// <summary>
        /// Validates a tag value for proper format and characters.
        /// </summary>
        /// <param name="tagValue">The tag value to validate.</param>
        /// <exception cref="ArgumentException">Thrown when the tag value is invalid.</exception>
        private static void ValidateTag(string tagValue)
        {
            ValidateTagNotNullOrEmpty(tagValue);
            ValidateTagLength(tagValue);
            ValidateTagStartsWithLetter(tagValue);
            ValidateTagContainsColon(tagValue);
            ValidateTagCharacters(tagValue);
        }

        /// <summary>
        /// Validates that the tag is not null or empty.
        /// </summary>
        /// <param name="tagValue">The tag to validate.</param>
        /// <exception cref="ArgumentException">Thrown when the tag is null or empty.</exception>
        private static void ValidateTagNotNullOrEmpty(string tagValue)
        {
            if (string.IsNullOrEmpty(tagValue))
            {
                throw new ArgumentException("Tags cannot contain null or empty values.", nameof(tagValue));
            }
        }

        /// <summary>
        /// Validates that the tag length is within the allowed limit.
        /// </summary>
        /// <param name="tagValue">The tag to validate.</param>
        /// <exception cref="ArgumentException">Thrown when the tag exceeds maximum length.</exception>
        private static void ValidateTagLength(string tagValue)
        {
            if (tagValue.Length > MaxTagLength)
            {
                throw new ArgumentException($"Tags cannot exceed {MaxTagLength} characters.", nameof(tagValue));
            }
        }

        /// <summary>
        /// Validates that the tag starts with a letter.
        /// </summary>
        /// <param name="tagValue">The tag to validate.</param>
        /// <exception cref="ArgumentException">Thrown when the tag doesn't start with a letter.</exception>
        private static void ValidateTagStartsWithLetter(string tagValue)
        {
            if (!char.IsLetter(tagValue[0]))
            {
                throw new ArgumentException("Tags must start with a letter.", nameof(tagValue));
            }
        }

        /// <summary>
        /// Validates that the tag contains at least one colon to delimit key and value.
        /// </summary>
        /// <param name="tagValue">The tag to validate.</param>
        /// <exception cref="ArgumentException">Thrown when the tag doesn't contain a colon.</exception>
        private static void ValidateTagContainsColon(string tagValue)
        {
            if (tagValue.IndexOf(":", StringComparison.Ordinal) < 0)
            {
                throw new ArgumentException("Tags must contain at least one colon (:) to delimit the tag key and value.", nameof(tagValue));
            }
        }

        /// <summary>
        /// Validates that all characters in the tag are allowed.
        /// </summary>
        /// <param name="tagValue">The tag to validate.</param>
        /// <exception cref="ArgumentException">Thrown when the tag contains invalid characters.</exception>
        private static void ValidateTagCharacters(string tagValue)
        {
            for (int i = 1; i < tagValue.Length; i++)
            {
                char c = tagValue[i];
                if (!IsValidTagCharacter(c))
                {
                    throw new ArgumentException($"Tag contains invalid character '{c}'. Only alphanumerics, underscores, minuses, colons, periods, and slashes are allowed.", nameof(tagValue));
                }
            }
        }

        /// <summary>
        /// Checks if a character is valid for use in a tag.
        /// </summary>
        /// <param name="c">The character to check.</param>
        /// <returns>True if the character is valid, false otherwise.</returns>
        private static bool IsValidTagCharacter(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == ':' || c == '.' || c == '/';
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current tag.
        /// </summary>
        /// <param name="obj">The object to compare with the current tag.</param>
        /// <returns>True if the specified object is equal to the current tag; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            return obj is Tag other
                ? string.Equals(Key, other.Key, StringComparison.Ordinal) && string.Equals(Value, other.Value, StringComparison.Ordinal)
                : obj is string stringValue && string.Equals(ToString(), stringValue, StringComparison.Ordinal);
        }

        /// <summary>
        /// Returns a hash code for the current tag.
        /// </summary>
        /// <returns>A hash code for the current tag.</returns>
        public override int GetHashCode()
        {
            return new { Key, Value }.GetHashCode();
        }

        /// <summary>
        /// Returns the string representation of the tag.
        /// </summary>
        /// <returns>The tags key and value as a string.</returns>
        public override string ToString()
        {
            return Key + ":" + Value;
        }
    }
}
