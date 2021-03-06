﻿// Copyright © 2013 onwards, Andrew Whewell
// All rights reserved.
//
// Redistribution and use of this software in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
//    * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//    * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//    * Neither the name of the author nor the names of the program's contributors may be used to endorse or promote products derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE AUTHORS OF THE SOFTWARE BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VirtualRadar.Interface;
using Test.Framework;

namespace Test.VirtualRadar.Interface
{
    [TestClass]
    public class PictureDetailTests
    {
        [TestMethod]
        public void PictureDetail_Constructor_Initialises_To_Known_State_And_Properties_Work()
        {
            var detail = new PictureDetail();
            TestUtilities.TestProperty(detail, r => r.FileName, null, "Ha");
            TestUtilities.TestProperty(detail, r => r.Height, 0, 12003);
            TestUtilities.TestProperty(detail, r => r.Width, 0, 2013);
            TestUtilities.TestProperty(detail, r => r.LastModifiedTime, default(DateTime), DateTime.UtcNow);
            TestUtilities.TestProperty(detail, r => r.Length, 0L, long.MaxValue);
        }

        [TestMethod]
        public void PictureDetail_Equals_Returns_True_When_Comparing_Identical_Objects()
        {
            var aircraft = new PictureDetail();
            Assert.AreEqual(true, aircraft.Equals(aircraft));
        }

        [TestMethod]
        public void PictureDetail_Equals_Returns_False_When_Comparing_Against_Null()
        {
            Assert.AreEqual(false, new PictureDetail().Equals(null));
        }

        [TestMethod]
        public void PictureDetail_Equals_Returns_False_When_Comparing_Against_Different_Object_Type()
        {
            Assert.AreEqual(false, new PictureDetail().Equals("Hello"));
        }

        [TestMethod]
        public void PictureDetail_Equals_Returns_True_When_Comparing_Against_Object_With_Identical_Properties()
        {
            TestUtilities.TestSimpleEquals(typeof(PictureDetail), true);
        }

        [TestMethod]
        public void PictureDetail_Equals_Returns_False_When_Comparing_Against_Object_With_Different_Properties()
        {
            TestUtilities.TestSimpleEquals(typeof(PictureDetail), false);
        }
    }
}
