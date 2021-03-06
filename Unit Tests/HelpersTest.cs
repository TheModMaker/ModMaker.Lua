// Copyright 2016 Jacob Trimble
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using ModMaker.Lua;
using NUnit.Framework;

namespace UnitTests {
  [TestFixture]
  public class HelpersTest {
    [Test]
    public void ParseNumber() {
      // TODO: Consider fixing the rounding errors.
      const double delta = 1e-6;
      Assert.AreEqual(123, Helpers.ParseNumber("123"), delta);
      Assert.AreEqual(123.456, Helpers.ParseNumber("123.456"), delta);
      Assert.AreEqual(0.456, Helpers.ParseNumber("0.456"), delta);
      Assert.AreEqual(.456, Helpers.ParseNumber(".456"), delta);
      Assert.AreEqual(0, Helpers.ParseNumber("0e5"), delta);
      Assert.AreEqual(0, Helpers.ParseNumber("0e-5"), delta);
      Assert.AreEqual(12e5, Helpers.ParseNumber("12e5"), delta);
      Assert.AreEqual(12e5, Helpers.ParseNumber("12E+5"), delta);
      Assert.AreEqual(122e-5, Helpers.ParseNumber("122E-5"), delta);
      Assert.AreEqual(1.12e1, Helpers.ParseNumber("1.12e1"), delta);
      Assert.AreEqual(.12e8, Helpers.ParseNumber(".12e8"), delta);

      Assert.AreEqual(0x123, Helpers.ParseNumber("0x123"));
      Assert.AreEqual(18.31103515625, Helpers.ParseNumber("0x12.4fa"), delta);
      Assert.AreEqual(0.656494140625, Helpers.ParseNumber("0x.a81"), delta);
      Assert.AreEqual(325.3125, Helpers.ParseNumber("0x14.55p4"), delta);
      Assert.AreEqual(4.625, Helpers.ParseNumber("0x.4ap4"), delta);
      Assert.AreEqual(4.546875, Helpers.ParseNumber("0x123p-6"), delta);
    }

    // TODO: Fix tests.

    /// <summary>
    /// Tests the Disposable method with an invalid method.
    /// </summary>
    /*[TestMethod]
    public void Disposable_Invalid() {
      try {
        Helpers.Disposable(null);
        Assert.Fail("Disposable should throw will null.");
      }
      catch (ArgumentNullException) { }
    }
    /// <summary>
    /// Tests the Disposable method with a valid method.
    /// </summary>
    [TestMethod]
    public void Disposable_Valid() {
      var dummy = false;
      var temp = Helpers.Disposable(() => {
        Assert.IsFalse(dummy, "Disposable called method twice.");
        dummy = true;
      });
      Assert.IsNotNull(temp, "Disposable returned null.");

      temp.Dispose();
      Assert.IsTrue(dummy, "Disposable did not call the method.");

      temp.Dispose();
    }

    /// <summary>
    /// Tests the ToDouble method with a null value.
    /// </summary>
    [TestMethod]
    public void ToDouble_Null() {
      var temp = Helpers.ToDouble(null);
      Assert.IsNull(temp, "ToDouble should convert null to null.");
    }
    /// <summary>
    /// Tests the ToDouble method with an object value.
    /// </summary>
    [TestMethod]
    public void ToDouble_Object() {
      var value = new object();
      var temp = Helpers.ToDouble(value);
      Assert.AreSame(value, temp, "ToDouble should give the same object back.");
    }
    /// <summary>
    /// Tests the ToDouble method with a string value.
    /// </summary>
    [TestMethod]
    public void ToDouble_String() {
      var value = "Foobar";
      var temp = Helpers.ToDouble(value);
      Assert.AreSame(value, temp, "ToDouble should give the same object back.");
    }
    /// <summary>
    /// Tests the ToDouble method with a pointer value.
    /// </summary>
    [TestMethod]
    public void ToDouble_Pointer() {
      var value = IntPtr.Zero;
      var temp = Helpers.ToDouble(value);
      Assert.AreEqual(value, temp, "ToDouble should give the same value back.");
    }
    /// <summary>
    /// Tests the ToDouble method with a double value.
    /// </summary>
    [TestMethod]
    public void ToDouble_Double() {
      var value = 2.6;
      var temp = Helpers.ToDouble(value);
      Assert.AreEqual(value, temp, "ToDouble should give the same value back.");
      Assert.IsInstanceOfType(temp, typeof(double), "ToDouble should give a double.");
    }
    /// <summary>
    /// Tests the ToDouble method with a integer value.
    /// </summary>
    [TestMethod]
    public void ToDouble_Int() {
      int value = 0;
      var temp = Helpers.ToDouble(value);
      Assert.AreEqual((double)value, temp, "ToDouble should give the same value");
      Assert.IsInstanceOfType(temp, typeof(double), "ToDouble should give a double.");
    }
    /// <summary>
    /// Tests the ToDouble method with a decimal value.
    /// </summary>
    [TestMethod]
    public void ToDouble_Dec() {
      decimal value = 0;
      var temp = Helpers.ToDouble(value);
      Assert.AreEqual((double)value, temp, "ToDouble should give the same value");
      Assert.IsInstanceOfType(temp, typeof(double), "ToDouble should give a double.");
    }

    /// <summary>
    /// Tests the FixArgs method with a null value.
    /// </summary>
    [TestMethod]
    public void FixArgs_NullZero() {
      var temp = Helpers.FixArgs(null, 0);
      Assert.IsNotNull(temp, "FixArgs should not return null.");
      Assert.AreEqual(0, temp.Length, "FixArgs should return a length of 0.");
    }
    /// <summary>
    /// Tests the FixArgs method with a null and negative value.
    /// </summary>
    [TestMethod]
    public void FixArgs_NullNegative() {
      var temp = Helpers.FixArgs(null, -4);
      Assert.IsNotNull(temp, "FixArgs should not return null.");
      Assert.AreEqual(0, temp.Length, "FixArgs should return a length of 0.");
    }
    /// <summary>
    /// Tests the FixArgs method with a null value.
    /// </summary>
    [TestMethod]
    public void FixArgs_NullPositive() {
      var temp = Helpers.FixArgs(null, 4);
      Assert.IsNotNull(temp, "FixArgs should not return null.");
      Assert.AreEqual(4, temp.Length, "FixArgs should return a length of 4.");

      foreach (var item in temp) {
          Assert.IsNull(item, "FixArgs items should all be null.");
      }
    }
    /// <summary>
    /// Tests the FixArgs method with an array and negative value.
    /// </summary>
    [TestMethod]
    public void FixArgs_Negative() {
      var arr = new object[3];
      var temp = Helpers.FixArgs(arr, -4);
      Assert.IsNotNull(temp, "FixArgs should not return null.");
      Assert.AreEqual(3, temp.Length, "FixArgs should return a length of 3.");
    }
    /// <summary>
    /// Tests the FixArgs method with an array and a smaller value.
    /// </summary>
    [TestMethod]
    public void FixArgs_Smaller() {
      var arr = new object[3];
      var temp = Helpers.FixArgs(arr, 1);
      Assert.IsNotNull(temp, "FixArgs should not return null.");
      Assert.AreEqual(3, temp.Length, "FixArgs should return a length of 3.");
    }
    /// <summary>
    /// Tests the FixArgs method with an array and a smaller value.
    /// </summary>
    [TestMethod]
    public void FixArgs_Bigger() {
      var arr = new object[3];
      var temp = Helpers.FixArgs(arr, 8);
      Assert.IsNotNull(temp, "FixArgs should not return null.");
      Assert.AreEqual(8, temp.Length, "FixArgs should return a length of 8.");
    }
    /// <summary>
    /// Tests the FixArgs method with an array with a MultipleReturn first.
    /// </summary>
    [TestMethod]
    public void FixArgs_MultiFirst() {
      var arr = new object[3];
      arr[0] = new MultipleReturn(new[] { 2, 4 });
      var temp = Helpers.FixArgs(arr, 1);

      Assert.IsNotNull(temp, "FixArgs should not return null.");
      Assert.AreEqual(3, temp.Length, "FixArgs should return a length of 3.");
    }
    /// <summary>
    /// Tests the FixArgs method with an array with a MultipleReturn at the end.
    /// </summary>
    [TestMethod]
    public void FixArgs_MultiEnd() {
      var arr = new object[3];
      arr[2] = new MultipleReturn(new[] { 2, 4 });
      var temp = Helpers.FixArgs(arr, 1);

      Assert.IsNotNull(temp, "FixArgs should not return null.");
      Assert.AreEqual(4.0, temp.Length, "FixArgs should return a length of 4.");
      Assert.AreEqual(2.0, temp[2]);
      Assert.AreEqual(4.0, temp[3]);
    }
    /// <summary>
    /// Tests the FixArgs method with an array with several MultipleReturns.
    /// </summary>
    [TestMethod]
    public void FixArgs_MultiMulti() {
      var arr = new object[3];
      arr[0] = new MultipleReturn(new[] { 1, 1, 1 });
      arr[2] = new MultipleReturn(new[] { 4, 4, 4 });
      var temp = Helpers.FixArgs(arr, 1);

      Assert.IsNotNull(temp, "FixArgs should not return null.");
      Assert.AreEqual(5.0, temp.Length, "FixArgs should return a length of 5.");
      Assert.AreEqual(1.0, temp[0], "FixArgs values are incorrect.");
      Assert.AreEqual(4.0, temp[2], "FixArgs values are incorrect.");
      Assert.AreEqual(4.0, temp[3], "FixArgs values are incorrect.");
      Assert.AreEqual(4.0, temp[4], "FixArgs values are incorrect.");
    }*/
  }
}
