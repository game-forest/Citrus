using System;
using System.CodeDom.Compiler;

namespace Lime.Source.Widgets.Animesh
{
	using EA = ExactArithmetic;

	[GeneratedCode(
		"https://github.com/Geri-Borbas/Triangle.NET/blob/master/Triangle.NET/Triangle/RobustPredicates.cs",
		"0e920cdce3c638e3f71df6de43df50a10228f18f"
	)]
	[System.Diagnostics.DebuggerStepThrough]
	public static unsafe class GeometricPredicates
	{
		// epsilon is equal to Math.Pow(2.0, -53) and is the largest power of
		// two that 1.0 + epsilon = 1.0.
		// NOTE: Don't confuse this with double.Epsilon.
		private const double Epsilon = 1.1102230246251565E-16;

		// Error bounds for orientation and incircle tests.
		private const double Resulterrbound = (3.0 + 8.0 * Epsilon) * Epsilon;
		private const double CcwerrboundA = (3.0 + 16.0 * Epsilon) * Epsilon;
		private const double ccwerrboundB = (2.0 + 12.0 * Epsilon) * Epsilon;
		private const double ccwerrboundC = (9.0 + 64.0 * Epsilon) * Epsilon * Epsilon;
		private const double iccerrboundA = (10.0 + 96.0 * Epsilon) * Epsilon;
		private const double iccerrboundB = (4.0 + 48.0 * Epsilon) * Epsilon;
		private const double iccerrboundC = (44.0 + 576.0 * Epsilon) * Epsilon * Epsilon;

		/// <summary>
		/// Non-robust approximate 2D orientation test.
		/// </summary>
		/// <param name="pax">x coordinate of pa.</param>
		/// <param name="pay">y coordinate of pa.</param>
		/// <param name="pbx">x coordinate of pb.</param>
		/// <param name="pby">y coordinate of pb.</param>
		/// <param name="pcx">x coordinate of pc.</param>
		/// <param name="pcy">y coordinate of pc.</param>
		/// <returns>a positive value if the points pa, pb, and pc occur
		/// in counterclockwise order; a negative value if they occur in
		/// clockwise order; and zero if they are collinear.
		/// The result is also a rough aproximation of twice the signed
		/// area of the triangle defined by the three points.</returns>
		/// <remarks>The implementation computed the determinant using simple double arithmetic.</remarks>
		public static double Orient2D(double pax, double pay, double pbx, double pby, double pcx, double pcy)
		{
			double acx, bcx, acy, bcy;

			acx = pax - pcx;
			bcx = pbx - pcx;
			acy = pay - pcy;
			bcy = pby - pcy;
			return acx * bcy - acy * bcx;
		}

		/// <summary>
		/// Robust approximate 2D orientation test.
		/// </summary>
		/// <param name="pax">x coordinate of pa.</param>
		/// <param name="pay">y coordinate of pa.</param>
		/// <param name="pbx">x coordinate of pb.</param>
		/// <param name="pby">y coordinate of pb.</param>
		/// <param name="pcx">x coordinate of pc.</param>
		/// <param name="pcy">y coordinate of pc.</param>
		/// <returns>a positive value if the points pa, pb, and pc occur
		/// in counterclockwise order; a negative value if they occur in
		/// clockwise order; and zero if they are collinear.
		/// The result is also a rough aproximation of twice the signed
		/// area of the triangle defined by the three points.</returns>
		/// <remarks>The implementation computed the determinant using simple double arithmetic.</remarks>
		public static double ExactOrient2D(double pax, double pay, double pbx, double pby, double pcx, double pcy)
		{
			double axby1, axcy1, bxcy1, bxay1, cxay1, cxby1;
			double axby0, axcy0, bxcy0, bxay0, cxay0, cxby0;
			double* aterms = stackalloc double[4];
			double* bterms = stackalloc double[4];
			double* cterms = stackalloc double[4];
			double aterms3, bterms3, cterms3;
			double* v = stackalloc double[8];
			double* w = stackalloc double[12];
			int vlength, wlength;

			EA.TwoProduct(pax, pby, out axby1, out axby0);
			EA.TwoProduct(pax, pcy, out axcy1, out axcy0);
			EA.TwoTwoDiff(axby1, axby0, axcy1, axcy0, out aterms3, out aterms[2], out aterms[1], out aterms[0]);
			aterms[3] = aterms3;

			EA.TwoProduct(pbx, pcy, out bxcy1, out bxcy0);
			EA.TwoProduct(pbx, pay, out bxay1, out bxay0);
			EA.TwoTwoDiff(bxcy1, bxcy0, bxay1, bxay0, out bterms3, out bterms[2], out bterms[1], out bterms[0]);
			bterms[3] = bterms3;

			EA.TwoProduct(pcx, pay, out cxay1, out cxay0);
			EA.TwoProduct(pcx, pby, out cxby1, out cxby0);
			EA.TwoTwoDiff(cxay1, cxay0, cxby1, cxby0, out cterms3, out cterms[2], out cterms[1], out cterms[0]);
			cterms[3] = cterms3;

			vlength = EA.FastExpansionSumZeroElim(4, aterms, 4, bterms, v);
			wlength = EA.FastExpansionSumZeroElim(vlength, v, 4, cterms, w);

			// In S. predicates.c, this returns the largest component:
			// return w[wlength - 1];
			// However, this is not stable due to the expansions not being unique,
			// So we return the summed estimate as the 'Exact' value.
			return EA.Estimate(wlength, w);
		}

		/// <summary>
		/// Adaptive, robust 2D orientation test.
		/// </summary>
		/// <param name="pax">x coordinate of pa.</param>
		/// <param name="pay">y coordinate of pa.</param>
		/// <param name="pbx">x coordinate of pb.</param>
		/// <param name="pby">y coordinate of pb.</param>
		/// <param name="pcx">x coordinate of pc.</param>
		/// <param name="pcy">y coordinate of pc.</param>
		/// <returns>a positive value if the points pa, pb, and pc occur
		/// in counterclockwise order; a negative value if they occur in
		/// clockwise order; and zero if they are collinear.
		/// The result is also an aproximation of twice the signed
		/// area of the triangle defined by the three points.</returns>
		public static double AdaptiveOrient2D(double pax, double pay, double pbx, double pby, double pcx, double pcy)
		{
			double detleft, detright, det;
			double detsum, errbound;

			detleft = (pax - pcx) * (pby - pcy);
			detright = (pay - pcy) * (pbx - pcx);
			det = detleft - detright;

			if (detleft > 0.0) {
				if (detright <= 0.0) {
					return det;
				} else {
					detsum = detleft + detright;
				}
			} else if (detleft < 0.0) {
				if (detright >= 0.0) {
					return det;
				} else {
					detsum = -detleft - detright;
				}
			} else {
				return det;
			}

			errbound = CcwerrboundA * detsum;
			if ((det >= errbound) || (-det >= errbound)) {
				return det;
			}

			return InternalAdaptiveOrient2D(pax, pay, pbx, pby, pcx, pcy, detsum);
		}

		// Internal adaptive continuation of AdaptiveOrient2D
		private static double InternalAdaptiveOrient2D(double pax, double pay, double pbx, double pby, double pcx, double pcy, double detsum)
		{
			double acx, acy, bcx, bcy;
			double acxtail, acytail, bcxtail, bcytail;
			double detleft, detright;
			double detlefttail, detrighttail;
			double det, errbound;
			double* b = stackalloc double[4];
			double* c1 = stackalloc double[8];
			double* c2 = stackalloc double[12];
			double* D = stackalloc double[16];
			double B3;
			int C1length, C2length, Dlength;
			double* u = stackalloc double[4];
			double u3;
			double s1, t1;
			double s0, t0;

			acx = pax - pcx;
			bcx = pbx - pcx;
			acy = pay - pcy;
			bcy = pby - pcy;

			EA.TwoProduct(acx, bcy, out detleft, out detlefttail);
			EA.TwoProduct(acy, bcx, out detright, out detrighttail);

			EA.TwoTwoDiff(detleft, detlefttail, detright, detrighttail,
						out B3, out b[2], out b[1], out b[0]);
			b[3] = B3;

			det = EA.Estimate(4, b);
			errbound = ccwerrboundB * detsum;
			if ((det >= errbound) || (-det >= errbound)) {
				return det;
			}

			EA.TwoDiffTail(pax, pcx, acx, out acxtail);
			EA.TwoDiffTail(pbx, pcx, bcx, out bcxtail);
			EA.TwoDiffTail(pay, pcy, acy, out acytail);
			EA.TwoDiffTail(pby, pcy, bcy, out bcytail);

			if ((acxtail == 0.0) && (acytail == 0.0)
				&& (bcxtail == 0.0) && (bcytail == 0.0)) {
				return det;
			}

			errbound = ccwerrboundC * detsum + Resulterrbound * Math.Abs(det);
			det += acx * bcytail + bcy * acxtail
				- (acy * bcxtail + bcx * acytail);
			if ((det >= errbound) || (-det >= errbound)) {
				return det;
			}

			EA.TwoProduct(acxtail, bcy, out s1, out s0);
			EA.TwoProduct(acytail, bcx, out t1, out t0);
			EA.TwoTwoDiff(s1, s0, t1, t0, out u3, out u[2], out u[1], out u[0]);
			u[3] = u3;
			C1length = EA.FastExpansionSumZeroElim(4, b, 4, u, c1);

			EA.TwoProduct(acx, bcytail, out s1, out s0);
			EA.TwoProduct(acy, bcxtail, out t1, out t0);
			EA.TwoTwoDiff(s1, s0, t1, t0, out u3, out u[2], out u[1], out u[0]);
			u[3] = u3;
			C2length = EA.FastExpansionSumZeroElim(C1length, c1, 4, u, c2);

			EA.TwoProduct(acxtail, bcytail, out s1, out s0);
			EA.TwoProduct(acytail, bcxtail, out t1, out t0);
			EA.TwoTwoDiff(s1, s0, t1, t0, out u3, out u[2], out u[1], out u[0]);
			u[3] = u3;
			Dlength = EA.FastExpansionSumZeroElim(C2length, c2, 4, u, D);

			return D[Dlength - 1];
		}

		// |pax pay pax^2+pay^2 1|
		// |pbx pby pbx^2+pby^2 1|
		// |pcx pcy pcx^2+pcy^2 1|
		// |pdx pdy pdx^2+pdy^2 1|

		/// <summary>
		/// Non-robust in circle test.
		/// </summary>
		/// <param name="pax">x coordinate of pa.</param>
		/// <param name="pay">y coordinate of pa.</param>
		/// <param name="pbx">x coordinate of pb.</param>
		/// <param name="pby">y coordinate of pb.</param>
		/// <param name="pcx">x coordinate of pc.</param>
		/// <param name="pcy">y coordinate of pc.</param>
		/// <param name="pdx">x coordinate of pd.</param>
		/// <param name="pdy">y coordinate of pd.</param>
		/// <returns>
		/// Return a positive value if the point pd lies inside the
		/// circle passing through pa, pb, and pc; a negative value if
		/// it lies outside; and zero if the four points are cocircular.
		/// The points pa, pb, and pc must be in counterclockwise
		/// order, or the sign of the result will be reversed.
		/// </returns>
		public static double InCircle(double pax, double pay, double pbx, double pby, double pcx, double pcy, double pdx, double pdy)
		{
			double adx, ady, bdx, bdy, cdx, cdy;
			double abdet, bcdet, cadet;
			double alift, blift, clift;

			adx = pax - pdx;
			ady = pay - pdy;
			bdx = pbx - pdx;
			bdy = pby - pdy;
			cdx = pcx - pdx;
			cdy = pcy - pdy;

			abdet = adx * bdy - bdx * ady;
			bcdet = bdx * cdy - cdx * bdy;
			cadet = cdx * ady - adx * cdy;
			alift = adx * adx + ady * ady;
			blift = bdx * bdx + bdy * bdy;
			clift = cdx * cdx + cdy * cdy;

			return alift * bcdet + blift * cadet + clift * abdet;
		}

		/// <summary>
		/// Robust in circle test.
		/// </summary>
		/// <param name="pax">x coordinate of pa.</param>
		/// <param name="pay">y coordinate of pa.</param>
		/// <param name="pbx">x coordinate of pb.</param>
		/// <param name="pby">y coordinate of pb.</param>
		/// <param name="pcx">x coordinate of pc.</param>
		/// <param name="pcy">y coordinate of pc.</param>
		/// <param name="pdx">x coordinate of pd.</param>
		/// <param name="pdy">y coordinate of pd.</param>
		/// <returns>
		/// Return a positive value if the point pd lies inside the
		/// circle passing through pa, pb, and pc; a negative value if
		/// it lies outside; and zero if the four points are cocircular.
		/// The points pa, pb, and pc must be in counterclockwise
		/// order, or the sign of the result will be reversed.
		/// </returns>
		internal static double ExactInCircle(double pax, double pay, double pbx, double pby, double pcx, double pcy, double pdx, double pdy)
		{
			double axby1, bxcy1, cxdy1, dxay1, axcy1, bxdy1;
			double bxay1, cxby1, dxcy1, axdy1, cxay1, dxby1;
			double axby0, bxcy0, cxdy0, dxay0, axcy0, bxdy0;
			double bxay0, cxby0, dxcy0, axdy0, cxay0, dxby0;
			double* ab = stackalloc double[4];
			double* bc = stackalloc double[4];
			double* cd = stackalloc double[4];
			double* da = stackalloc double[4];
			double* ac = stackalloc double[4];
			double* bd = stackalloc double[4];
			double* temp8 = stackalloc double[8];
			int templen;
			double* abc = stackalloc double[12];
			double* bcd = stackalloc double[12];
			double* cda = stackalloc double[12];
			double* dab = stackalloc double[12];
			int abclen, bcdlen, cdalen, dablen;
			double* det24x = stackalloc double[24];
			double* det24y = stackalloc double[24];
			double* det48x = stackalloc double[48];
			double* det48y = stackalloc double[48];
			int xlen, ylen;
			double* adet = stackalloc double[96];
			double* bdet = stackalloc double[96];
			double* cdet = stackalloc double[96];
			double* ddet = stackalloc double[96];
			int alen, blen, clen, dlen;
			double* abdet = stackalloc double[192];
			double* cddet = stackalloc double[192];
			int ablen, cdlen;
			double* deter = stackalloc double[384];
			int deterlen;
			int i;

			EA.TwoProduct(pax, pby, out axby1, out axby0);
			EA.TwoProduct(pbx, pay, out bxay1, out bxay0);
			EA.TwoTwoDiff(axby1, axby0, bxay1, bxay0, out ab[3], out ab[2], out ab[1], out ab[0]);

			EA.TwoProduct(pbx, pcy, out bxcy1, out bxcy0);
			EA.TwoProduct(pcx, pby, out cxby1, out cxby0);
			EA.TwoTwoDiff(bxcy1, bxcy0, cxby1, cxby0, out bc[3], out bc[2], out bc[1], out bc[0]);

			EA.TwoProduct(pcx, pdy, out cxdy1, out cxdy0);
			EA.TwoProduct(pdx, pcy, out dxcy1, out dxcy0);
			EA.TwoTwoDiff(cxdy1, cxdy0, dxcy1, dxcy0, out cd[3], out cd[2], out cd[1], out cd[0]);

			EA.TwoProduct(pdx, pay, out dxay1, out dxay0);
			EA.TwoProduct(pax, pdy, out axdy1, out axdy0);
			EA.TwoTwoDiff(dxay1, dxay0, axdy1, axdy0, out da[3], out da[2], out da[1], out da[0]);

			EA.TwoProduct(pax, pcy, out axcy1, out axcy0);
			EA.TwoProduct(pcx, pay, out cxay1, out cxay0);
			EA.TwoTwoDiff(axcy1, axcy0, cxay1, cxay0, out ac[3], out ac[2], out ac[1], out ac[0]);

			EA.TwoProduct(pbx, pdy, out bxdy1, out bxdy0);
			EA.TwoProduct(pdx, pby, out dxby1, out dxby0);
			EA.TwoTwoDiff(bxdy1, bxdy0, dxby1, dxby0, out bd[3], out bd[2], out bd[1], out bd[0]);

			templen = EA.FastExpansionSumZeroElim(4, cd, 4, da, temp8);
			cdalen = EA.FastExpansionSumZeroElim(templen, temp8, 4, ac, cda);
			templen = EA.FastExpansionSumZeroElim(4, da, 4, ab, temp8);
			dablen = EA.FastExpansionSumZeroElim(templen, temp8, 4, bd, dab);
			for (i = 0; i < 4; i++) {
				bd[i] = -bd[i];
				ac[i] = -ac[i];
			}
			templen = EA.FastExpansionSumZeroElim(4, ab, 4, bc, temp8);
			abclen = EA.FastExpansionSumZeroElim(templen, temp8, 4, ac, abc);
			templen = EA.FastExpansionSumZeroElim(4, bc, 4, cd, temp8);
			bcdlen = EA.FastExpansionSumZeroElim(templen, temp8, 4, bd, bcd);

			xlen = EA.ScaleExpansionZeroElim(bcdlen, bcd, pax, det24x);
			xlen = EA.ScaleExpansionZeroElim(xlen, det24x, pax, det48x);
			ylen = EA.ScaleExpansionZeroElim(bcdlen, bcd, pay, det24y);
			ylen = EA.ScaleExpansionZeroElim(ylen, det24y, pay, det48y);
			alen = EA.FastExpansionSumZeroElim(xlen, det48x, ylen, det48y, adet);

			xlen = EA.ScaleExpansionZeroElim(cdalen, cda, pbx, det24x);
			xlen = EA.ScaleExpansionZeroElim(xlen, det24x, -pbx, det48x);
			ylen = EA.ScaleExpansionZeroElim(cdalen, cda, pby, det24y);
			ylen = EA.ScaleExpansionZeroElim(ylen, det24y, -pby, det48y);
			blen = EA.FastExpansionSumZeroElim(xlen, det48x, ylen, det48y, bdet);

			xlen = EA.ScaleExpansionZeroElim(dablen, dab, pcx, det24x);
			xlen = EA.ScaleExpansionZeroElim(xlen, det24x, pcx, det48x);
			ylen = EA.ScaleExpansionZeroElim(dablen, dab, pcy, det24y);
			ylen = EA.ScaleExpansionZeroElim(ylen, det24y, pcy, det48y);
			clen = EA.FastExpansionSumZeroElim(xlen, det48x, ylen, det48y, cdet);

			xlen = EA.ScaleExpansionZeroElim(abclen, abc, pdx, det24x);
			xlen = EA.ScaleExpansionZeroElim(xlen, det24x, -pdx, det48x);
			ylen = EA.ScaleExpansionZeroElim(abclen, abc, pdy, det24y);
			ylen = EA.ScaleExpansionZeroElim(ylen, det24y, -pdy, det48y);
			dlen = EA.FastExpansionSumZeroElim(xlen, det48x, ylen, det48y, ddet);

			ablen = EA.FastExpansionSumZeroElim(alen, adet, blen, bdet, abdet);
			cdlen = EA.FastExpansionSumZeroElim(clen, cdet, dlen, ddet, cddet);
			deterlen = EA.FastExpansionSumZeroElim(ablen, abdet, cdlen, cddet, deter);

			// In S. predicates.c, this returns the largest component:
			// deter[deterlen - 1];
			// However, this is not stable due to the expansions not being unique (even for ZeroElim),
			// So we return the summed estimate as the 'Exact' value.
			return EA.Estimate(deterlen, deter);
		}

		/// <summary>
		/// Adaptive, robust in circle test.
		/// </summary>
		/// <param name="pax">x coordinate of pa.</param>
		/// <param name="pay">y coordinate of pa.</param>
		/// <param name="pbx">x coordinate of pb.</param>
		/// <param name="pby">y coordinate of pb.</param>
		/// <param name="pcx">x coordinate of pc.</param>
		/// <param name="pcy">y coordinate of pc.</param>
		/// <param name="pdx">x coordinate of pd.</param>
		/// <param name="pdy">y coordinate of pd.</param>
		/// <returns>
		/// Return a positive value if the point pd lies inside the
		/// circle passing through pa, pb, and pc; a negative value if
		/// it lies outside; and zero if the four points are cocircular.
		/// The points pa, pb, and pc must be in counterclockwise
		/// order, or the sign of the result will be reversed.
		/// </returns>
		public static double AdaptiveInCircle(double pax, double pay, double pbx, double pby, double pcx, double pcy, double pdx, double pdy)
		{
			double adx, bdx, cdx, ady, bdy, cdy;
			double bdxcdy, cdxbdy, cdxady, adxcdy, adxbdy, bdxady;
			double alift, blift, clift;
			double det;
			double permanent, errbound;

			adx = pax - pdx;
			bdx = pbx - pdx;
			cdx = pcx - pdx;
			ady = pay - pdy;
			bdy = pby - pdy;
			cdy = pcy - pdy;

			bdxcdy = bdx * cdy;
			cdxbdy = cdx * bdy;
			alift = adx * adx + ady * ady;

			cdxady = cdx * ady;
			adxcdy = adx * cdy;
			blift = bdx * bdx + bdy * bdy;

			adxbdy = adx * bdy;
			bdxady = bdx * ady;
			clift = cdx * cdx + cdy * cdy;

			det = alift * (bdxcdy - cdxbdy)
				+ blift * (cdxady - adxcdy)
				+ clift * (adxbdy - bdxady);

			permanent = (Math.Abs(bdxcdy) + Math.Abs(cdxbdy)) * alift
					+ (Math.Abs(cdxady) + Math.Abs(adxcdy)) * blift
					+ (Math.Abs(adxbdy) + Math.Abs(bdxady)) * clift;
			errbound = iccerrboundA * permanent;
			if ((det > errbound) || (-det > errbound)) {
				return det;
			}

			return InternalAdaptiveInCircle(pax, pay, pbx, pby, pcx, pcy, pdx, pdy, permanent);
		}

		// Adaptive continuation of AdaptiveInCircle
		private static double InternalAdaptiveInCircle(double pax, double pay, double pbx, double pby, double pcx, double pcy, double pdx, double pdy, double permanent)
		{
			double adx, bdx, cdx, ady, bdy, cdy;
			double det, errbound;

			double bdxcdy1, cdxbdy1, cdxady1, adxcdy1, adxbdy1, bdxady1;
			double bdxcdy0, cdxbdy0, cdxady0, adxcdy0, adxbdy0, bdxady0;
			double* bc = stackalloc double[4];
			double* ca = stackalloc double[4];
			double* ab = stackalloc double[4];
			double bc3, ca3, ab3;
			double* axbc = stackalloc double[8];
			double* axxbc = stackalloc double[16];
			double* aybc = stackalloc double[8];
			double* ayybc = stackalloc double[16];
			double* adet = stackalloc double[32];
			int axbclen, axxbclen, aybclen, ayybclen, alen;
			double* bxca = stackalloc double[8];
			double* bxxca = stackalloc double[16];
			double* byca = stackalloc double[8];
			double* byyca = stackalloc double[16];
			double* bdet = stackalloc double[32];
			int bxcalen, bxxcalen, bycalen, byycalen, blen;
			double* cxab = stackalloc double[8];
			double* cxxab = stackalloc double[16];
			double* cyab = stackalloc double[8];
			double* cyyab = stackalloc double[16];
			double* cdet = stackalloc double[32];
			int cxablen, cxxablen, cyablen, cyyablen, clen;
			double* abdet = stackalloc double[64];
			int ablen;
			double* fin1 = stackalloc double[1152];
			double* fin2 = stackalloc double[1152];
			double* finnow, finother, finswap;
			int finlength;

			double adxtail, bdxtail, cdxtail, adytail, bdytail, cdytail;
			double adxadx1, adyady1, bdxbdx1, bdybdy1, cdxcdx1, cdycdy1;
			double adxadx0, adyady0, bdxbdx0, bdybdy0, cdxcdx0, cdycdy0;
			double* aa = stackalloc double[4];
			double* bb = stackalloc double[4];
			double* cc = stackalloc double[4];
			double aa3, bb3, cc3;
			double ti1, tj1;
			double ti0, tj0;
			double* u = stackalloc double[4];
			double* v = stackalloc double[4];
			double u3, v3;
			double* temp8 = stackalloc double[8];
			double* temp16a = stackalloc double[16];
			double* temp16b = stackalloc double[16];
			double* temp16c = stackalloc double[16];
			double* temp32a = stackalloc double[32];
			double* temp32b = stackalloc double[32];
			double* temp48 = stackalloc double[48];
			double* temp64 = stackalloc double[64];
			int temp8len, temp16alen, temp16blen, temp16clen;
			int temp32alen, temp32blen, temp48len, temp64len;
			double* axtbb = stackalloc double[8];
			double* axtcc = stackalloc double[8];
			double* aytbb = stackalloc double[8];
			double* aytcc = stackalloc double[8];
			int axtbblen, axtcclen, aytbblen, aytcclen;
			double* bxtaa = stackalloc double[8];
			double* bxtcc = stackalloc double[8];
			double* bytaa = stackalloc double[8];
			double* bytcc = stackalloc double[8];
			int bxtaalen, bxtcclen, bytaalen, bytcclen;
			double* cxtaa = stackalloc double[8];
			double* cxtbb = stackalloc double[8];
			double* cytaa = stackalloc double[8];
			double* cytbb = stackalloc double[8];
			int cxtaalen, cxtbblen, cytaalen, cytbblen;
			double* axtbc = stackalloc double[8];
			double* aytbc = stackalloc double[8];
			double* bxtca = stackalloc double[8];
			double* bytca = stackalloc double[8];
			double* cxtab = stackalloc double[8];
			double* cytab = stackalloc double[8];
			int axtbclen, aytbclen, bxtcalen, bytcalen, cxtablen, cytablen;
			double* axtbct = stackalloc double[16];
			double* aytbct = stackalloc double[16];
			double* bxtcat = stackalloc double[16];
			double* bytcat = stackalloc double[16];
			double* cxtabt = stackalloc double[16];
			double* cytabt = stackalloc double[16];
			int axtbctlen, aytbctlen, bxtcatlen, bytcatlen, cxtabtlen, cytabtlen;
			double* axtbctt = stackalloc double[8];
			double* aytbctt = stackalloc double[8];
			double* bxtcatt = stackalloc double[8];
			double* bytcatt = stackalloc double[8];
			double* cxtabtt = stackalloc double[8];
			double* cytabtt = stackalloc double[8];
			int axtbcttlen, aytbcttlen, bxtcattlen, bytcattlen, cxtabttlen, cytabttlen;
			double* abt = stackalloc double[8];
			double* bct = stackalloc double[8];
			double* cat = stackalloc double[8];
			int abtlen, bctlen, catlen;
			double* abtt = stackalloc double[4];
			double* bctt = stackalloc double[4];
			double* catt = stackalloc double[4];
			int abttlen, bcttlen, cattlen;
			double abtt3, bctt3, catt3;
			double negate;

			// RobustGeometry.NET - Additional initialization, for C# compiler,
			//                      to a value that should cause an error if used by accident.
			axtbclen = 9999;
			aytbclen = 9999;
			bxtcalen = 9999;
			bytcalen = 9999;
			cxtablen = 9999;
			cytablen = 9999;

			adx = pax - pdx;
			bdx = pbx - pdx;
			cdx = pcx - pdx;
			ady = pay - pdy;
			bdy = pby - pdy;
			cdy = pcy - pdy;

			EA.TwoProduct(bdx, cdy, out bdxcdy1, out bdxcdy0);
			EA.TwoProduct(cdx, bdy, out cdxbdy1, out cdxbdy0);
			EA.TwoTwoDiff(bdxcdy1, bdxcdy0, cdxbdy1, cdxbdy0, out bc3, out bc[2], out bc[1], out bc[0]);
			bc[3] = bc3;
			axbclen = EA.ScaleExpansionZeroElim(4, bc, adx, axbc);
			axxbclen = EA.ScaleExpansionZeroElim(axbclen, axbc, adx, axxbc);
			aybclen = EA.ScaleExpansionZeroElim(4, bc, ady, aybc);
			ayybclen = EA.ScaleExpansionZeroElim(aybclen, aybc, ady, ayybc);
			alen = EA.FastExpansionSumZeroElim(axxbclen, axxbc, ayybclen, ayybc, adet);

			EA.TwoProduct(cdx, ady, out cdxady1, out cdxady0);
			EA.TwoProduct(adx, cdy, out adxcdy1, out adxcdy0);
			EA.TwoTwoDiff(cdxady1, cdxady0, adxcdy1, adxcdy0, out ca3, out ca[2], out ca[1], out ca[0]);
			ca[3] = ca3;
			bxcalen = EA.ScaleExpansionZeroElim(4, ca, bdx, bxca);
			bxxcalen = EA.ScaleExpansionZeroElim(bxcalen, bxca, bdx, bxxca);
			bycalen = EA.ScaleExpansionZeroElim(4, ca, bdy, byca);
			byycalen = EA.ScaleExpansionZeroElim(bycalen, byca, bdy, byyca);
			blen = EA.FastExpansionSumZeroElim(bxxcalen, bxxca, byycalen, byyca, bdet);

			EA.TwoProduct(adx, bdy, out adxbdy1, out adxbdy0);
			EA.TwoProduct(bdx, ady, out bdxady1, out bdxady0);
			EA.TwoTwoDiff(adxbdy1, adxbdy0, bdxady1, bdxady0, out ab3, out ab[2], out ab[1], out ab[0]);
			ab[3] = ab3;
			cxablen = EA.ScaleExpansionZeroElim(4, ab, cdx, cxab);
			cxxablen = EA.ScaleExpansionZeroElim(cxablen, cxab, cdx, cxxab);
			cyablen = EA.ScaleExpansionZeroElim(4, ab, cdy, cyab);
			cyyablen = EA.ScaleExpansionZeroElim(cyablen, cyab, cdy, cyyab);
			clen = EA.FastExpansionSumZeroElim(cxxablen, cxxab, cyyablen, cyyab, cdet);

			ablen = EA.FastExpansionSumZeroElim(alen, adet, blen, bdet, abdet);
			finlength = EA.FastExpansionSumZeroElim(ablen, abdet, clen, cdet, fin1);

			det = EA.Estimate(finlength, fin1);
			errbound = iccerrboundB * permanent;
			if ((det >= errbound) || (-det >= errbound)) {
				return det;
			}

			EA.TwoDiffTail(pax, pdx, adx, out adxtail);
			EA.TwoDiffTail(pay, pdy, ady, out adytail);
			EA.TwoDiffTail(pbx, pdx, bdx, out bdxtail);
			EA.TwoDiffTail(pby, pdy, bdy, out bdytail);
			EA.TwoDiffTail(pcx, pdx, cdx, out cdxtail);
			EA.TwoDiffTail(pcy, pdy, cdy, out cdytail);
			if ((adxtail == 0.0) && (bdxtail == 0.0) && (cdxtail == 0.0)
				&& (adytail == 0.0) && (bdytail == 0.0) && (cdytail == 0.0)) {
				return det;
			}

			errbound = iccerrboundC * permanent + Resulterrbound * Math.Abs(det);
			det += (adx * adx + ady * ady) * (bdx * cdytail + cdy * bdxtail
												- (bdy * cdxtail + cdx * bdytail))
					+ 2.0 * (adx * adxtail + ady * adytail) * (bdx * cdy - bdy * cdx)
				+ ((bdx * bdx + bdy * bdy) * (cdx * adytail + ady * cdxtail
												- (cdy * adxtail + adx * cdytail))
					+ 2.0 * (bdx * bdxtail + bdy * bdytail) * (cdx * ady - cdy * adx))
				+ ((cdx * cdx + cdy * cdy) * (adx * bdytail + bdy * adxtail
												- (ady * bdxtail + bdx * adytail))
					+ 2.0 * (cdx * cdxtail + cdy * cdytail) * (adx * bdy - ady * bdx));
			if ((det >= errbound) || (-det >= errbound)) {
				return det;
			}

			finnow = fin1;
			finother = fin2;

			if ((bdxtail != 0.0) || (bdytail != 0.0)
				|| (cdxtail != 0.0) || (cdytail != 0.0)) {
				EA.Square(adx, out adxadx1, out adxadx0);
				EA.Square(ady, out adyady1, out adyady0);
				EA.TwoTwoSum(adxadx1, adxadx0, adyady1, adyady0, out aa3, out aa[2], out aa[1], out aa[0]);
				aa[3] = aa3;
			}
			if ((cdxtail != 0.0) || (cdytail != 0.0)
				|| (adxtail != 0.0) || (adytail != 0.0)) {
				EA.Square(bdx, out bdxbdx1, out bdxbdx0);
				EA.Square(bdy, out bdybdy1, out bdybdy0);
				EA.TwoTwoSum(bdxbdx1, bdxbdx0, bdybdy1, bdybdy0, out bb3, out bb[2], out bb[1], out bb[0]);
				bb[3] = bb3;
			}
			if ((adxtail != 0.0) || (adytail != 0.0)
				|| (bdxtail != 0.0) || (bdytail != 0.0)) {
				EA.Square(cdx, out cdxcdx1, out cdxcdx0);
				EA.Square(cdy, out cdycdy1, out cdycdy0);
				EA.TwoTwoSum(cdxcdx1, cdxcdx0, cdycdy1, cdycdy0, out cc3, out cc[2], out cc[1], out cc[0]);
				cc[3] = cc3;
			}

			if (adxtail != 0.0) {
				axtbclen = EA.ScaleExpansionZeroElim(4, bc, adxtail, axtbc);
				temp16alen = EA.ScaleExpansionZeroElim(axtbclen, axtbc, 2.0 * adx, temp16a);

				axtcclen = EA.ScaleExpansionZeroElim(4, cc, adxtail, axtcc);
				temp16blen = EA.ScaleExpansionZeroElim(axtcclen, axtcc, bdy, temp16b);

				axtbblen = EA.ScaleExpansionZeroElim(4, bb, adxtail, axtbb);
				temp16clen = EA.ScaleExpansionZeroElim(axtbblen, axtbb, -cdy, temp16c);

				temp32alen = EA.FastExpansionSumZeroElim(temp16alen, temp16a, temp16blen, temp16b, temp32a);
				temp48len = EA.FastExpansionSumZeroElim(temp16clen, temp16c, temp32alen, temp32a, temp48);
				finlength = EA.FastExpansionSumZeroElim(finlength, finnow, temp48len, temp48, finother);
				finswap = finnow; finnow = finother; finother = finswap;
			}
			if (adytail != 0.0) {
				aytbclen = EA.ScaleExpansionZeroElim(4, bc, adytail, aytbc);
				temp16alen = EA.ScaleExpansionZeroElim(aytbclen, aytbc, 2.0 * ady, temp16a);

				aytbblen = EA.ScaleExpansionZeroElim(4, bb, adytail, aytbb);
				temp16blen = EA.ScaleExpansionZeroElim(aytbblen, aytbb, cdx, temp16b);

				aytcclen = EA.ScaleExpansionZeroElim(4, cc, adytail, aytcc);
				temp16clen = EA.ScaleExpansionZeroElim(aytcclen, aytcc, -bdx, temp16c);

				temp32alen = EA.FastExpansionSumZeroElim(temp16alen, temp16a, temp16blen, temp16b, temp32a);
				temp48len = EA.FastExpansionSumZeroElim(temp16clen, temp16c, temp32alen, temp32a, temp48);
				finlength = EA.FastExpansionSumZeroElim(finlength, finnow, temp48len, temp48, finother);
				finswap = finnow; finnow = finother; finother = finswap;
			}
			if (bdxtail != 0.0) {
				bxtcalen = EA.ScaleExpansionZeroElim(4, ca, bdxtail, bxtca);
				temp16alen = EA.ScaleExpansionZeroElim(bxtcalen, bxtca, 2.0 * bdx, temp16a);

				bxtaalen = EA.ScaleExpansionZeroElim(4, aa, bdxtail, bxtaa);
				temp16blen = EA.ScaleExpansionZeroElim(bxtaalen, bxtaa, cdy, temp16b);

				bxtcclen = EA.ScaleExpansionZeroElim(4, cc, bdxtail, bxtcc);
				temp16clen = EA.ScaleExpansionZeroElim(bxtcclen, bxtcc, -ady, temp16c);

				temp32alen = EA.FastExpansionSumZeroElim(temp16alen, temp16a, temp16blen, temp16b, temp32a);
				temp48len = EA.FastExpansionSumZeroElim(temp16clen, temp16c, temp32alen, temp32a, temp48);
				finlength = EA.FastExpansionSumZeroElim(finlength, finnow, temp48len, temp48, finother);
				finswap = finnow; finnow = finother; finother = finswap;
			}
			if (bdytail != 0.0) {
				bytcalen = EA.ScaleExpansionZeroElim(4, ca, bdytail, bytca);
				temp16alen = EA.ScaleExpansionZeroElim(bytcalen, bytca, 2.0 * bdy, temp16a);

				bytcclen = EA.ScaleExpansionZeroElim(4, cc, bdytail, bytcc);
				temp16blen = EA.ScaleExpansionZeroElim(bytcclen, bytcc, adx, temp16b);

				bytaalen = EA.ScaleExpansionZeroElim(4, aa, bdytail, bytaa);
				temp16clen = EA.ScaleExpansionZeroElim(bytaalen, bytaa, -cdx, temp16c);

				temp32alen = EA.FastExpansionSumZeroElim(temp16alen, temp16a, temp16blen, temp16b, temp32a);
				temp48len = EA.FastExpansionSumZeroElim(temp16clen, temp16c, temp32alen, temp32a, temp48);
				finlength = EA.FastExpansionSumZeroElim(finlength, finnow, temp48len, temp48, finother);
				finswap = finnow; finnow = finother; finother = finswap;
			}
			if (cdxtail != 0.0) {
				cxtablen = EA.ScaleExpansionZeroElim(4, ab, cdxtail, cxtab);
				temp16alen = EA.ScaleExpansionZeroElim(cxtablen, cxtab, 2.0 * cdx, temp16a);

				cxtbblen = EA.ScaleExpansionZeroElim(4, bb, cdxtail, cxtbb);
				temp16blen = EA.ScaleExpansionZeroElim(cxtbblen, cxtbb, ady, temp16b);

				cxtaalen = EA.ScaleExpansionZeroElim(4, aa, cdxtail, cxtaa);
				temp16clen = EA.ScaleExpansionZeroElim(cxtaalen, cxtaa, -bdy, temp16c);

				temp32alen = EA.FastExpansionSumZeroElim(temp16alen, temp16a, temp16blen, temp16b, temp32a);
				temp48len = EA.FastExpansionSumZeroElim(temp16clen, temp16c, temp32alen, temp32a, temp48);
				finlength = EA.FastExpansionSumZeroElim(finlength, finnow, temp48len, temp48, finother);
				finswap = finnow; finnow = finother; finother = finswap;
			}
			if (cdytail != 0.0) {
				cytablen = EA.ScaleExpansionZeroElim(4, ab, cdytail, cytab);
				temp16alen = EA.ScaleExpansionZeroElim(cytablen, cytab, 2.0 * cdy, temp16a);

				cytaalen = EA.ScaleExpansionZeroElim(4, aa, cdytail, cytaa);
				temp16blen = EA.ScaleExpansionZeroElim(cytaalen, cytaa, bdx, temp16b);

				cytbblen = EA.ScaleExpansionZeroElim(4, bb, cdytail, cytbb);
				temp16clen = EA.ScaleExpansionZeroElim(cytbblen, cytbb, -adx, temp16c);

				temp32alen = EA.FastExpansionSumZeroElim(temp16alen, temp16a, temp16blen, temp16b, temp32a);
				temp48len = EA.FastExpansionSumZeroElim(temp16clen, temp16c, temp32alen, temp32a, temp48);
				finlength = EA.FastExpansionSumZeroElim(finlength, finnow, temp48len, temp48, finother);
				finswap = finnow; finnow = finother; finother = finswap;
			}

			if ((adxtail != 0.0) || (adytail != 0.0)) {
				if ((bdxtail != 0.0) || (bdytail != 0.0)
					|| (cdxtail != 0.0) || (cdytail != 0.0)) {
					EA.TwoProduct(bdxtail, cdy, out ti1, out ti0);
					EA.TwoProduct(bdx, cdytail, out tj1, out tj0);
					EA.TwoTwoSum(ti1, ti0, tj1, tj0, out u3, out u[2], out u[1], out u[0]);
					u[3] = u3;
					negate = -bdy;
					EA.TwoProduct(cdxtail, negate, out ti1, out ti0);
					negate = -bdytail;
					EA.TwoProduct(cdx, negate, out tj1, out tj0);
					EA.TwoTwoSum(ti1, ti0, tj1, tj0, out v3, out v[2], out v[1], out v[0]);
					v[3] = v3;
					bctlen = EA.FastExpansionSumZeroElim(4, u, 4, v, bct);

					EA.TwoProduct(bdxtail, cdytail, out ti1, out ti0);
					EA.TwoProduct(cdxtail, bdytail, out tj1, out tj0);
					EA.TwoTwoDiff(ti1, ti0, tj1, tj0, out bctt3, out bctt[2], out bctt[1], out bctt[0]);
					bctt[3] = bctt3;
					bcttlen = 4;
				} else {
					bct[0] = 0.0;
					bctlen = 1;
					bctt[0] = 0.0;
					bcttlen = 1;
				}

				if (adxtail != 0.0) {
					temp16alen = EA.ScaleExpansionZeroElim(axtbclen, axtbc, adxtail, temp16a);
					axtbctlen = EA.ScaleExpansionZeroElim(bctlen, bct, adxtail, axtbct);
					temp32alen = EA.ScaleExpansionZeroElim(axtbctlen, axtbct, 2.0 * adx, temp32a);
					temp48len = EA.FastExpansionSumZeroElim(temp16alen, temp16a, temp32alen, temp32a, temp48);
					finlength = EA.FastExpansionSumZeroElim(finlength, finnow, temp48len, temp48, finother);
					finswap = finnow; finnow = finother; finother = finswap;
					if (bdytail != 0.0) {
						temp8len = EA.ScaleExpansionZeroElim(4, cc, adxtail, temp8);
						temp16alen = EA.ScaleExpansionZeroElim(temp8len, temp8, bdytail, temp16a);
						finlength = EA.FastExpansionSumZeroElim(finlength, finnow, temp16alen, temp16a, finother);
						finswap = finnow; finnow = finother; finother = finswap;
					}
					if (cdytail != 0.0) {
						temp8len = EA.ScaleExpansionZeroElim(4, bb, -adxtail, temp8);
						temp16alen = EA.ScaleExpansionZeroElim(temp8len, temp8, cdytail, temp16a);
						finlength = EA.FastExpansionSumZeroElim(finlength, finnow, temp16alen, temp16a, finother);
						finswap = finnow; finnow = finother; finother = finswap;
					}

					temp32alen = EA.ScaleExpansionZeroElim(axtbctlen, axtbct, adxtail, temp32a);
					axtbcttlen = EA.ScaleExpansionZeroElim(bcttlen, bctt, adxtail, axtbctt);
					temp16alen = EA.ScaleExpansionZeroElim(axtbcttlen, axtbctt, 2.0 * adx, temp16a);
					temp16blen = EA.ScaleExpansionZeroElim(axtbcttlen, axtbctt, adxtail, temp16b);
					temp32blen = EA.FastExpansionSumZeroElim(temp16alen, temp16a, temp16blen, temp16b, temp32b);
					temp64len = EA.FastExpansionSumZeroElim(temp32alen, temp32a, temp32blen, temp32b, temp64);
					finlength = EA.FastExpansionSumZeroElim(finlength, finnow, temp64len, temp64, finother);
					finswap = finnow; finnow = finother; finother = finswap;
				}
				if (adytail != 0.0) {
					temp16alen = EA.ScaleExpansionZeroElim(aytbclen, aytbc, adytail, temp16a);
					aytbctlen = EA.ScaleExpansionZeroElim(bctlen, bct, adytail, aytbct);
					temp32alen = EA.ScaleExpansionZeroElim(aytbctlen, aytbct, 2.0 * ady, temp32a);
					temp48len = EA.FastExpansionSumZeroElim(temp16alen, temp16a, temp32alen, temp32a, temp48);
					finlength = EA.FastExpansionSumZeroElim(finlength, finnow, temp48len, temp48, finother);
					finswap = finnow; finnow = finother; finother = finswap;

					temp32alen = EA.ScaleExpansionZeroElim(aytbctlen, aytbct, adytail, temp32a);
					aytbcttlen = EA.ScaleExpansionZeroElim(bcttlen, bctt, adytail, aytbctt);
					temp16alen = EA.ScaleExpansionZeroElim(aytbcttlen, aytbctt, 2.0 * ady, temp16a);
					temp16blen = EA.ScaleExpansionZeroElim(aytbcttlen, aytbctt, adytail, temp16b);
					temp32blen = EA.FastExpansionSumZeroElim(temp16alen, temp16a, temp16blen, temp16b, temp32b);
					temp64len = EA.FastExpansionSumZeroElim(temp32alen, temp32a, temp32blen, temp32b, temp64);
					finlength = EA.FastExpansionSumZeroElim(finlength, finnow, temp64len, temp64, finother);
					finswap = finnow; finnow = finother; finother = finswap;
				}
			}
			if ((bdxtail != 0.0) || (bdytail != 0.0)) {
				if ((cdxtail != 0.0) || (cdytail != 0.0)
					|| (adxtail != 0.0) || (adytail != 0.0)) {
					EA.TwoProduct(cdxtail, ady, out ti1, out ti0);
					EA.TwoProduct(cdx, adytail, out tj1, out tj0);
					EA.TwoTwoSum(ti1, ti0, tj1, tj0, out u3, out u[2], out u[1], out u[0]);
					u[3] = u3;
					negate = -cdy;
					EA.TwoProduct(adxtail, negate, out ti1, out ti0);
					negate = -cdytail;
					EA.TwoProduct(adx, negate, out tj1, out tj0);
					EA.TwoTwoSum(ti1, ti0, tj1, tj0, out v3, out v[2], out v[1], out v[0]);
					v[3] = v3;
					catlen = EA.FastExpansionSumZeroElim(4, u, 4, v, cat);

					EA.TwoProduct(cdxtail, adytail, out ti1, out ti0);
					EA.TwoProduct(adxtail, cdytail, out tj1, out tj0);
					EA.TwoTwoDiff(ti1, ti0, tj1, tj0, out catt3, out catt[2], out catt[1], out catt[0]);
					catt[3] = catt3;
					cattlen = 4;
				} else {
					cat[0] = 0.0;
					catlen = 1;
					catt[0] = 0.0;
					cattlen = 1;
				}

				if (bdxtail != 0.0) {
					temp16alen = EA.ScaleExpansionZeroElim(bxtcalen, bxtca, bdxtail, temp16a);
					bxtcatlen = EA.ScaleExpansionZeroElim(catlen, cat, bdxtail, bxtcat);
					temp32alen = EA.ScaleExpansionZeroElim(bxtcatlen, bxtcat, 2.0 * bdx, temp32a);
					temp48len = EA.FastExpansionSumZeroElim(temp16alen, temp16a, temp32alen, temp32a, temp48);
					finlength = EA.FastExpansionSumZeroElim(finlength, finnow, temp48len, temp48, finother);
					finswap = finnow; finnow = finother; finother = finswap;
					if (cdytail != 0.0) {
						temp8len = EA.ScaleExpansionZeroElim(4, aa, bdxtail, temp8);
						temp16alen = EA.ScaleExpansionZeroElim(temp8len, temp8, cdytail, temp16a);
						finlength = EA.FastExpansionSumZeroElim(finlength, finnow, temp16alen, temp16a, finother);
						finswap = finnow; finnow = finother; finother = finswap;
					}
					if (adytail != 0.0) {
						temp8len = EA.ScaleExpansionZeroElim(4, cc, -bdxtail, temp8);
						temp16alen = EA.ScaleExpansionZeroElim(temp8len, temp8, adytail, temp16a);
						finlength = EA.FastExpansionSumZeroElim(finlength, finnow, temp16alen, temp16a, finother);
						finswap = finnow; finnow = finother; finother = finswap;
					}

					temp32alen = EA.ScaleExpansionZeroElim(bxtcatlen, bxtcat, bdxtail, temp32a);
					bxtcattlen = EA.ScaleExpansionZeroElim(cattlen, catt, bdxtail, bxtcatt);
					temp16alen = EA.ScaleExpansionZeroElim(bxtcattlen, bxtcatt, 2.0 * bdx, temp16a);
					temp16blen = EA.ScaleExpansionZeroElim(bxtcattlen, bxtcatt, bdxtail, temp16b);
					temp32blen = EA.FastExpansionSumZeroElim(temp16alen, temp16a, temp16blen, temp16b, temp32b);
					temp64len = EA.FastExpansionSumZeroElim(temp32alen, temp32a, temp32blen, temp32b, temp64);
					finlength = EA.FastExpansionSumZeroElim(finlength, finnow, temp64len, temp64, finother);
					finswap = finnow; finnow = finother; finother = finswap;
				}
				if (bdytail != 0.0) {
					temp16alen = EA.ScaleExpansionZeroElim(bytcalen, bytca, bdytail, temp16a);
					bytcatlen = EA.ScaleExpansionZeroElim(catlen, cat, bdytail, bytcat);
					temp32alen = EA.ScaleExpansionZeroElim(bytcatlen, bytcat, 2.0 * bdy, temp32a);
					temp48len = EA.FastExpansionSumZeroElim(temp16alen, temp16a, temp32alen, temp32a, temp48);
					finlength = EA.FastExpansionSumZeroElim(finlength, finnow, temp48len, temp48, finother);
					finswap = finnow; finnow = finother; finother = finswap;

					temp32alen = EA.ScaleExpansionZeroElim(bytcatlen, bytcat, bdytail, temp32a);
					bytcattlen = EA.ScaleExpansionZeroElim(cattlen, catt, bdytail, bytcatt);
					temp16alen = EA.ScaleExpansionZeroElim(bytcattlen, bytcatt, 2.0 * bdy, temp16a);
					temp16blen = EA.ScaleExpansionZeroElim(bytcattlen, bytcatt, bdytail, temp16b);
					temp32blen = EA.FastExpansionSumZeroElim(temp16alen, temp16a, temp16blen, temp16b, temp32b);
					temp64len = EA.FastExpansionSumZeroElim(temp32alen, temp32a, temp32blen, temp32b, temp64);
					finlength = EA.FastExpansionSumZeroElim(finlength, finnow, temp64len, temp64, finother);
					finswap = finnow; finnow = finother; finother = finswap;
				}
			}
			if ((cdxtail != 0.0) || (cdytail != 0.0)) {
				if ((adxtail != 0.0) || (adytail != 0.0)
					|| (bdxtail != 0.0) || (bdytail != 0.0)) {
					EA.TwoProduct(adxtail, bdy, out ti1, out ti0);
					EA.TwoProduct(adx, bdytail, out tj1, out tj0);
					EA.TwoTwoSum(ti1, ti0, tj1, tj0, out u3, out u[2], out u[1], out u[0]);
					u[3] = u3;
					negate = -ady;
					EA.TwoProduct(bdxtail, negate, out ti1, out ti0);
					negate = -adytail;
					EA.TwoProduct(bdx, negate, out tj1, out tj0);
					EA.TwoTwoSum(ti1, ti0, tj1, tj0, out v3, out v[2], out v[1], out v[0]);
					v[3] = v3;
					abtlen = EA.FastExpansionSumZeroElim(4, u, 4, v, abt);

					EA.TwoProduct(adxtail, bdytail, out ti1, out ti0);
					EA.TwoProduct(bdxtail, adytail, out tj1, out tj0);
					EA.TwoTwoDiff(ti1, ti0, tj1, tj0, out abtt3, out abtt[2], out abtt[1], out abtt[0]);
					abtt[3] = abtt3;
					abttlen = 4;
				} else {
					abt[0] = 0.0;
					abtlen = 1;
					abtt[0] = 0.0;
					abttlen = 1;
				}

				if (cdxtail != 0.0) {
					temp16alen = EA.ScaleExpansionZeroElim(cxtablen, cxtab, cdxtail, temp16a);
					cxtabtlen = EA.ScaleExpansionZeroElim(abtlen, abt, cdxtail, cxtabt);
					temp32alen = EA.ScaleExpansionZeroElim(cxtabtlen, cxtabt, 2.0 * cdx, temp32a);
					temp48len = EA.FastExpansionSumZeroElim(temp16alen, temp16a, temp32alen, temp32a, temp48);
					finlength = EA.FastExpansionSumZeroElim(finlength, finnow, temp48len, temp48, finother);
					finswap = finnow; finnow = finother; finother = finswap;
					if (adytail != 0.0) {
						temp8len = EA.ScaleExpansionZeroElim(4, bb, cdxtail, temp8);
						temp16alen = EA.ScaleExpansionZeroElim(temp8len, temp8, adytail, temp16a);
						finlength = EA.FastExpansionSumZeroElim(finlength, finnow, temp16alen, temp16a, finother);
						finswap = finnow; finnow = finother; finother = finswap;
					}
					if (bdytail != 0.0) {
						temp8len = EA.ScaleExpansionZeroElim(4, aa, -cdxtail, temp8);
						temp16alen = EA.ScaleExpansionZeroElim(temp8len, temp8, bdytail, temp16a);
						finlength = EA.FastExpansionSumZeroElim(finlength, finnow, temp16alen, temp16a, finother);
						finswap = finnow; finnow = finother; finother = finswap;
					}

					temp32alen = EA.ScaleExpansionZeroElim(cxtabtlen, cxtabt, cdxtail, temp32a);
					cxtabttlen = EA.ScaleExpansionZeroElim(abttlen, abtt, cdxtail, cxtabtt);
					temp16alen = EA.ScaleExpansionZeroElim(cxtabttlen, cxtabtt, 2.0 * cdx, temp16a);
					temp16blen = EA.ScaleExpansionZeroElim(cxtabttlen, cxtabtt, cdxtail, temp16b);
					temp32blen = EA.FastExpansionSumZeroElim(temp16alen, temp16a, temp16blen, temp16b, temp32b);
					temp64len = EA.FastExpansionSumZeroElim(temp32alen, temp32a, temp32blen, temp32b, temp64);
					finlength = EA.FastExpansionSumZeroElim(finlength, finnow, temp64len, temp64, finother);
					finswap = finnow; finnow = finother; finother = finswap;
				}
				if (cdytail != 0.0) {
					temp16alen = EA.ScaleExpansionZeroElim(cytablen, cytab, cdytail, temp16a);
					cytabtlen = EA.ScaleExpansionZeroElim(abtlen, abt, cdytail, cytabt);
					temp32alen = EA.ScaleExpansionZeroElim(cytabtlen, cytabt, 2.0 * cdy, temp32a);
					temp48len = EA.FastExpansionSumZeroElim(temp16alen, temp16a, temp32alen, temp32a, temp48);
					finlength = EA.FastExpansionSumZeroElim(finlength, finnow, temp48len, temp48, finother);
					finswap = finnow; finnow = finother; finother = finswap;

					temp32alen = EA.ScaleExpansionZeroElim(cytabtlen, cytabt, cdytail, temp32a);
					cytabttlen = EA.ScaleExpansionZeroElim(abttlen, abtt, cdytail, cytabtt);
					temp16alen = EA.ScaleExpansionZeroElim(cytabttlen, cytabtt, 2.0 * cdy, temp16a);
					temp16blen = EA.ScaleExpansionZeroElim(cytabttlen, cytabtt, cdytail, temp16b);
					temp32blen = EA.FastExpansionSumZeroElim(temp16alen, temp16a, temp16blen, temp16b, temp32b);
					temp64len = EA.FastExpansionSumZeroElim(temp32alen, temp32a, temp32blen, temp32b, temp64);
					finlength = EA.FastExpansionSumZeroElim(finlength, finnow, temp64len, temp64, finother);
					finswap = finnow; finnow = finother; finother = finswap;
				}
			}

			return finnow[finlength - 1];
		}
	}
}
