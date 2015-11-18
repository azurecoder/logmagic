﻿using System;

namespace LogMagic
{
   public interface ILog
   {
      void D(string format, params object[] parameters);

      void E(string format, params object[] parameters);

      void I(string format, params object[] parameters);

      void W(string format, params object[] parameters);
   }
}
