using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using com.csutil.model;
using DiffMatchPatch;
using Xunit;

namespace com.csutil.tests {

    public class StringExtensionTests {

        public StringExtensionTests(Xunit.Abstractions.ITestOutputHelper logger) { logger.UseAsLoggingOutput(); }

        [Fact]
        public void StringExtension_Examples() {

            string myString = null;
            // If the string is null this will not throw a nullpointer exception:
            Assert.True(myString.IsNullOrEmpty());

            myString = "abc";
            Assert.False(myString.IsNullOrEmpty());

            // myString.Substring(..) examples:
            Assert.Equal("bc", myString.Substring(1, "d", includeEnd: true));
            Assert.Equal("bc", myString.Substring(1, "c", includeEnd: true));
            Assert.Equal("ab", myString.Substring("c", includeEnd: false));
            Assert.Equal("bc", myString.Substring(1, "abc", includeEnd: true));
            Assert.Equal("bc", myString.Substring(1, "abc", includeEnd: false));
            Assert.Equal("bc", myString.Substring(1, "d", includeEnd: false));

            // myString.SubstringAfter(..) examples:
            myString = "[{a}]-[{b}]";
            Assert.Equal("a}]-[{b}]", myString.SubstringAfter("{"));
            Assert.Equal("{b}]", myString.SubstringAfter("[", startFromBack: true));
            Assert.Throws<IndexOutOfRangeException>(() => { myString.SubstringAfter("("); });

            // Often SubstringAfter and Substring are used in combination:
            myString = "[(abc)]";
            Assert.Equal("abc", myString.SubstringAfter("(").Substring(")", includeEnd: false));

            // Use myString.With(..) as a short form of string.Format(..):
            myString = "<{0}, {1}>".With("A", "B");
            Assert.Equal("<A, B>", myString);

        }

        [Fact]
        public void StringExtension_RegexExamples() {

            // Check the structure of a string by providing a regex:
            Assert.True("abc".IsRegexMatch("a*"));

            Assert.True("Abc".IsRegexMatch("[A-Z][a-z][a-z]"));
            Assert.False("joe".IsRegexMatch("[A-Z][a-z][a-z]"));
            Assert.True("hat".IsRegexMatch(".at"));
            Assert.False("joe".IsRegexMatch(".at"));
            Assert.True("joe".IsRegexMatch("[!aeiou]*"));

            Assert.True("lalala".IsRegexMatch("(la)+"));

            Assert.True("YES".IsRegexMatch("(YES|MAYBE|NO)"));

            Assert.True("anna123".IsRegexMatch(RegexTemplates.USERNAME));
            Assert.True("Anna_123".IsRegexMatch(RegexTemplates.USERNAME));
            Assert.True("aa@bb.com".IsRegexMatch(RegexTemplates.EMAIL_ADDRESS));
            Assert.False("a@a@bb.com".IsRegexMatch(RegexTemplates.EMAIL_ADDRESS));

            Assert.False("".IsRegexMatch(RegexTemplates.NON_EMPTY_STRING));
            Assert.False(" ".IsRegexMatch(RegexTemplates.NON_EMPTY_STRING));
            Assert.False("  ".IsRegexMatch(RegexTemplates.NON_EMPTY_STRING));
            Assert.True("x".IsRegexMatch(RegexTemplates.NON_EMPTY_STRING));
            Assert.True(" x".IsRegexMatch(RegexTemplates.NON_EMPTY_STRING));
            Assert.True("x ".IsRegexMatch(RegexTemplates.NON_EMPTY_STRING));
            Assert.True(" x ".IsRegexMatch(RegexTemplates.NON_EMPTY_STRING));

            string nullString = null;
            Assert.False(nullString.IsRegexMatch("x"));
            Assert.Throws<ArgumentException>(() => { "x".IsRegexMatch(nullString); });
            Assert.Throws<ArgumentException>(() => { "x".IsRegexMatch(""); });


            const string minMaxCharLength = "^.{2,4}$";
            Assert.False("a".IsRegexMatch(minMaxCharLength));
            Assert.True("ab".IsRegexMatch(minMaxCharLength));
            Assert.True("abcd".IsRegexMatch(minMaxCharLength));
            Assert.False("abcde".IsRegexMatch(minMaxCharLength));

            string and1 = RegexUtil.CombineViaAnd(
                RegexTemplates.HAS_NUMBER,
                RegexTemplates.HAS_UPPERCASE);
            Assert.False("a".IsRegexMatch(and1));
            Assert.False("A".IsRegexMatch(and1));
            Assert.False("1".IsRegexMatch(and1));
            Assert.True("1A".IsRegexMatch(and1));
            Assert.True("A1".IsRegexMatch(and1));

            string and2 = RegexUtil.CombineViaAnd(
                RegexTemplates.HAS_NUMBER,
                RegexTemplates.HAS_LOWERCASE,
                RegexTemplates.HAS_SPECIAL_CHAR,
                RegexTemplates.HAS_UPPERCASE);
            Assert.False("Aa1".IsRegexMatch(and2));
            Assert.False("!1A".IsRegexMatch(and2));
            Assert.True("Aa1!".IsRegexMatch(and2));
            Assert.True("!a1A".IsRegexMatch(and2));

            string and3 = RegexUtil.CombineViaAnd(
                            RegexTemplates.EMAIL_ADDRESS,
                            RegexTemplates.HAS_LOWERCASE);
            Assert.False("a@b".IsRegexMatch(and3));
            Assert.True("a@b.com".IsRegexMatch(and3));
            Assert.False("A@B.COM".IsRegexMatch(and3));
            Assert.False("a@b.com@c".IsRegexMatch(and3));

            string or1 = RegexUtil.CombineViaOr(and1, and2, and3);
            Log.d("or regex: " + or1);
            Assert.False("a@b".IsRegexMatch(or1));
            Assert.False("a@b1".IsRegexMatch(or1));
            Assert.True("a@b.com".IsRegexMatch(or1)); //and3
            Assert.True("a@bA1".IsRegexMatch(or1)); // and2
            Assert.True("abbA1".IsRegexMatch(or1)); // and1
            Assert.False("abb1".IsRegexMatch(or1));

        }

        [Fact]
        public void StringEncryption_Examples() {

            var myString = "some text..";

            // Encrypt myString with the password "123":
            var myEncryptedString = myString.Encrypt("123");

            // The encrypted string is different to myString:
            Assert.NotEqual(myString, myEncryptedString);
            // Encrypting with a different password results into another encrypted string:
            Assert.NotEqual(myEncryptedString, myString.Encrypt("124"));

            // Decrypt the encrypted string back with the correct password:
            Assert.Equal(myString, myEncryptedString.Decrypt("123"));

            // Using the wrong password results in an exception:
            Assert.Throws<CryptographicException>(() => {
                Assert.NotEqual(myString, myEncryptedString.Decrypt("124"));
            });

        }

        [Fact]
        public void StringDiffMatchPatch_Examples() {

            var originalText = "Hi, im a very long text";
            var editedText_1 = "Hi, i'm a very long text!";
            var editedText_2 = "Hi, im not such a long text";
            var expectedText = "Hi, i'm not such a long text!";

            var merge = MergeText.Merge(originalText, editedText_1, editedText_2);
            Assert.Equal(expectedText, merge.mergeResult);
            foreach (var patch in merge.patches) { Assert.True(patch.Value); } // All patches were successful

            // diff_match_patch can also provide a detailed difference analysis:
            diff_match_patch dmp = new diff_match_patch();
            List<Diff> diff = dmp.diff_main(originalText, editedText_1);
            // The first section until the ' was unchanged:
            Assert.Equal("Hi, i", diff.First().text);
            Assert.Equal(Operation.EQUAL, diff.First().operation);
            // The last change was the insert of a !:
            Assert.Equal("!", diff.Last().text);
            Assert.Equal(Operation.INSERT, diff.Last().operation);

        }

    }

}