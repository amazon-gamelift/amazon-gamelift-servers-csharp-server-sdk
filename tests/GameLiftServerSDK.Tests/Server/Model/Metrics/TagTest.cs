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

namespace Aws.GameLift.Tests.Server.Model.Metrics
{
    using System;
    using Aws.GameLift.Server.Model.Metrics;
    using NUnit.Framework;

    [TestFixture]
    public class TagTest
    {
        [Test]
        public void GIVEN_valid_tag_with_colon_WHEN_tag_created_THEN_value_is_set_correctly()
        {
            // GIVEN
            string expectedValue = "validTag:value123";

            // WHEN
            var tag = new Tag(expectedValue);

            // THEN
            Assert.AreEqual("validTag", tag.Key);
            Assert.AreEqual("value123", tag.Value);
            Assert.AreEqual(expectedValue, tag.ToString());
        }

        [Test]
        public void GIVEN_tag_with_allowed_characters_WHEN_tag_created_THEN_tag_is_valid()
        {
            // GIVEN
            string validTag = "tag_name-value:key.path/resource";

            // WHEN & THEN
            Assert.DoesNotThrow(() => new Tag(validTag));
        }

        [Test]
        public void GIVEN_tag_starting_with_letter_and_containing_colon_WHEN_tag_created_THEN_tag_is_valid()
        {
            // GIVEN
            string validTag = "environment:production";

            // WHEN & THEN
            Assert.DoesNotThrow(() => new Tag(validTag));
        }

        [Test]
        public void GIVEN_tag_with_multiple_colons_WHEN_tag_created_THEN_tag_is_valid()
        {
            // GIVEN
            string validTag = "namespace:service:endpoint";

            // WHEN & THEN
            Assert.DoesNotThrow(() => new Tag(validTag));
        }

        [Test]
        public void GIVEN_tag_at_max_length_with_colon_WHEN_tag_created_THEN_tag_is_valid()
        {
            // GIVEN
            string validTag = "a:" + new string('b', 197); // 200 characters total

            // WHEN & THEN
            Assert.DoesNotThrow(() => new Tag(validTag));
        }

        [Test]
        public void GIVEN_null_tag_WHEN_tag_created_THEN_throws_argument_exception()
        {
            // GIVEN
            string nullTag = null;

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentException>(() => new Tag(nullTag));
            Assert.That(exception.Message, Does.Contain("Tags cannot contain null or empty values"));
            Assert.AreEqual("tagValue", exception.ParamName);
        }

        [Test]
        public void GIVEN_empty_tag_WHEN_tag_created_THEN_throws_argument_exception()
        {
            // GIVEN
            string emptyTag = string.Empty;

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentException>(() => new Tag(emptyTag));
            Assert.That(exception.Message, Does.Contain("Tags cannot contain null or empty values"));
            Assert.AreEqual("tagValue", exception.ParamName);
        }

        [Test]
        public void GIVEN_tag_exceeding_max_length_WHEN_tag_created_THEN_throws_argument_exception()
        {
            // GIVEN
            string longTag = "a:" + new string('b', 199); // 201 characters total

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentException>(() => new Tag(longTag));
            Assert.That(exception.Message, Does.Contain("Tags cannot exceed 200 characters"));
            Assert.AreEqual("tagValue", exception.ParamName);
        }

        [Test]
        public void GIVEN_tag_starting_with_number_WHEN_tag_created_THEN_throws_argument_exception()
        {
            // GIVEN
            string invalidTag = "1invalidTag:value";

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentException>(() => new Tag(invalidTag));
            Assert.That(exception.Message, Does.Contain("Tags must start with a letter"));
            Assert.AreEqual("tagValue", exception.ParamName);
        }

        [Test]
        public void GIVEN_tag_starting_with_underscore_WHEN_tag_created_THEN_throws_argument_exception()
        {
            // GIVEN
            string invalidTag = "_invalidTag:value";

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentException>(() => new Tag(invalidTag));
            Assert.That(exception.Message, Does.Contain("Tags must start with a letter"));
            Assert.AreEqual("tagValue", exception.ParamName);
        }

        [Test]
        public void GIVEN_tag_starting_with_colon_WHEN_tag_created_THEN_throws_argument_exception()
        {
            // GIVEN
            string invalidTag = ":invalidTag";

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentException>(() => new Tag(invalidTag));
            Assert.That(exception.Message, Does.Contain("Tags must start with a letter"));
            Assert.AreEqual("tagValue", exception.ParamName);
        }

        [Test]
        public void GIVEN_tag_without_colon_WHEN_tag_created_THEN_throws_argument_exception()
        {
            // GIVEN
            string invalidTag = "tagWithoutColon";

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentException>(() => new Tag(invalidTag));
            Assert.That(exception.Message, Does.Contain("Tags must contain at least one colon (:) to delimit the tag key and value"));
            Assert.AreEqual("tagValue", exception.ParamName);
        }

        [Test]
        public void GIVEN_single_letter_tag_without_colon_WHEN_tag_created_THEN_throws_argument_exception()
        {
            // GIVEN
            string invalidTag = "a";

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentException>(() => new Tag(invalidTag));
            Assert.That(exception.Message, Does.Contain("Tags must contain at least one colon (:) to delimit the tag key and value"));
            Assert.AreEqual("tagValue", exception.ParamName);
        }

        [Test]
        public void GIVEN_tag_with_colon_at_end_WHEN_tag_created_THEN_tag_is_valid()
        {
            // GIVEN
            string validTag = "key:";

            // WHEN & THEN
            Assert.DoesNotThrow(() => new Tag(validTag));
        }

        [Test]
        public void GIVEN_tag_with_colon_at_second_position_WHEN_tag_created_THEN_tag_is_valid()
        {
            // GIVEN
            string validTag = "a:value";

            // WHEN & THEN
            Assert.DoesNotThrow(() => new Tag(validTag));
        }

        [Test]
        public void GIVEN_tag_with_space_WHEN_tag_created_THEN_throws_argument_exception()
        {
            // GIVEN
            string invalidTag = "invalid tag:value";

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentException>(() => new Tag(invalidTag));
            Assert.That(exception.Message, Does.Contain("Tag contains invalid character ' '"));
            Assert.AreEqual("tagValue", exception.ParamName);
        }

        [Test]
        public void GIVEN_tag_with_exclamation_mark_WHEN_tag_created_THEN_throws_argument_exception()
        {
            // GIVEN
            string invalidTag = "invalid!tag:value";

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentException>(() => new Tag(invalidTag));
            Assert.That(exception.Message, Does.Contain("Tag contains invalid character '!'"));
            Assert.AreEqual("tagValue", exception.ParamName);
        }

        [Test]
        public void GIVEN_tag_with_at_symbol_WHEN_tag_created_THEN_throws_argument_exception()
        {
            // GIVEN
            string invalidTag = "invalid@tag:value";

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentException>(() => new Tag(invalidTag));
            Assert.That(exception.Message, Does.Contain("Tag contains invalid character '@'"));
            Assert.AreEqual("tagValue", exception.ParamName);
        }

        [Test]
        public void GIVEN_tag_with_hash_symbol_WHEN_tag_created_THEN_throws_argument_exception()
        {
            // GIVEN
            string invalidTag = "invalid#tag:value";

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentException>(() => new Tag(invalidTag));
            Assert.That(exception.Message, Does.Contain("Tag contains invalid character '#'"));
            Assert.AreEqual("tagValue", exception.ParamName);
        }

        [Test]
        public void GIVEN_valid_string_value_WHEN_implicitly_converted_to_tag_THEN_tag_created_with_value()
        {
            // GIVEN
            string stringValue = "testTag:value";

            // WHEN
            Tag tag = stringValue;

            // THEN
            Assert.AreEqual("testTag", tag.Key);
            Assert.AreEqual("value", tag.Value);
            Assert.AreEqual(stringValue, tag.ToString());
        }

        [Test]
        public void GIVEN_tag_WHEN_implicitly_converted_to_string_THEN_string_contains_tag_value()
        {
            // GIVEN
            string expectedValue = "testTag:value";
            var tag = new Tag(expectedValue);

            // WHEN
            string stringValue = tag;

            // THEN
            Assert.AreEqual(expectedValue, stringValue);
        }

        [Test]
        public void GIVEN_invalid_string_without_colon_WHEN_implicitly_converted_to_tag_THEN_throws_argument_exception()
        {
            // GIVEN
            string invalidString = "invalidTag";

            // WHEN & THEN
#pragma warning disable S1481
            Assert.Throws<ArgumentException>(() => { Tag tag = invalidString; });
#pragma warning restore S1481
        }

        [Test]
        public void GIVEN_invalid_string_starting_with_number_WHEN_implicitly_converted_to_tag_THEN_throws_argument_exception()
        {
            // GIVEN
            string invalidString = "1invalidTag:value";

            // WHEN & THEN
#pragma warning disable S1481
            Assert.Throws<ArgumentException>(() => { Tag tag = invalidString; });
#pragma warning restore S1481
        }

        [Test]
        public void GIVEN_valid_key_and_value_WHEN_tag_created_with_two_arg_ctor_THEN_properties_are_set_correctly()
        {
            // GIVEN
            string expectedKey = "validTag";
            string expectedValue = "value123";
            string expectedString = expectedKey + ":" + expectedValue;

            // WHEN
            var tag = new Tag(expectedKey, expectedValue);

            // THEN
            Assert.AreEqual(expectedKey, tag.Key);
            Assert.AreEqual(expectedValue, tag.Value);
            Assert.AreEqual(expectedString, tag.ToString());
        }

        [Test]
        public void GIVEN_null_key_WHEN_tag_created_with_two_arg_ctor_THEN_throws_argument_exception()
        {
            // GIVEN
            string key = null;
            string value = "value";

            // WHEN & THEN
            var ex = Assert.Throws<ArgumentException>(() => new Tag(key, value));
            StringAssert.Contains("null or empty", ex.Message);
        }

        [Test]
        public void GIVEN_null_value_WHEN_tag_created_with_two_arg_ctor_THEN_throws_argument_exception()
        {
            // GIVEN
            string key = "validTag";
            string value = null;

            // WHEN & THEN
            var ex = Assert.Throws<ArgumentException>(() => new Tag(key, value));
            StringAssert.Contains("null or empty", ex.Message);
        }

        [Test]
        public void GIVEN_invalid_key_or_value_WHEN_tag_created_with_two_arg_ctor_THEN_throws_argument_exception()
        {
            // GIVEN
            string key = "1invalid"; // does not start with letter
            string value = "value";

            // WHEN & THEN
            var ex = Assert.Throws<ArgumentException>(() => new Tag(key, value));
            StringAssert.Contains("start with a letter", ex.Message);
        }

        [Test]
        public void GIVEN_key_and_value_exceeding_max_length_WHEN_tag_created_with_two_arg_ctor_THEN_throws_argument_exception()
        {
            // GIVEN
            string key = new string('a', 201);
            string value = "value";

            // WHEN & THEN
            var ex = Assert.Throws<ArgumentException>(() => new Tag(key, value));
            StringAssert.Contains("exceed", ex.Message);
        }
    }
}
