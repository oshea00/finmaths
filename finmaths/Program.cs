using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Accord.Controls;
using Accord.Math;
using Accord.Math.Optimization;
using Accord.Statistics.Models.Regression.Fitting;
using Accord.Statistics.Visualizations;
using Deedle;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using Accord.Statistics;
using Accord;
using System.IO;

namespace finmaths
{
    class Program
    {
        static string TOKEN;
        static string APIURL;

        static void Main(string[] args)
        {
            if (!getenv(args))
                return;

            blackLitterman();

            Console.WriteLine("Press key to quit.");
            Console.ReadKey();
        }

        static void blackLitterman()
        {
            var data = load_data();
            var totcap = data.caps.Sum();
            var capwgts = data.caps.Multiply(1 / totcap); // cap weighted portfolio weights
            // MVO 
            var returns = asset_returns_and_covariances(data.prices);
            var perf = port_mean_var(capwgts, returns.returns, returns.cov);
            var rfrate = 0.015;
            var res1 = optimize_frontier(returns.returns, returns.cov, rfrate);
            //res1.FrontierMean.Dump();
            var stds1 = res1.FrontierVar.Select(v => Math.Sqrt(v)).ToArray();
            //ScatterplotBox.Show("Frontier",stds1,res1.FrontierMean);
            //$"MVO Tangent Portfolio mean = {res1.TangentMean} variance = {res1.TangentVar}".Dump();
            display_weights("MVO Weighted",res1, data.names);

            // Black-Litterman
            var lmb = (perf.mean - rfrate) / perf.variance;
            var pi = returns.cov.Multiply(lmb).Dot(capwgts);
            var res2 = optimize_frontier(pi.Add(rfrate), returns.cov, rfrate);
            var stds2 = res1.FrontierVar.Select(v => Math.Sqrt(v)).ToArray();
            //ScatterplotBox.Show("Frontier", stds2, res2.FrontierMean);
            display_weights("Cap Weighted",res2, data.names);

            var views = new List<(string tik1, string op, string tik2, double retdif)> {
                ("MSFT", ">", "GE", 0.50),
                ("AAPL", "<", "JNJ", 0.55)
            };
            var qandp = create_views_and_link_matrices(data.names, views);
            var tau = 0.025; // scaling factor

            // Calculate omega - uncertainty matrix about views
            var omega = qandp.Plink.Multiply(tau).Dot(returns.cov).Dot(qandp.Plink.Transpose());

            // Calculate equilibrium excess returns with views incorporated
            var dotTauC = returns.cov.Multiply(tau);
            var TransP = qandp.Plink.Transpose();
            var InvOmega = omega.Inverse();
            var sub_a = dotTauC.Inverse();
            var sub_b = TransP.Dot(InvOmega).Dot(qandp.Plink);
            var sub_c = dotTauC.Inverse().Dot(pi);
            var sub_d = TransP.Dot(InvOmega).Dot(qandp.Qviews);
            var piAdj = sub_a.Add(sub_b).Inverse().Dot(sub_c.Add(sub_d));
            // piAdj.Dump();
            var res3 = optimize_frontier(pi.Add(rfrate), returns.cov, rfrate);
            var stds3 = res3.FrontierVar.Select(v => Math.Sqrt(v)).ToArray();
            display_weights("Sentiment Weighted",res3, data.names);
            //ScatterplotBox.Show("Frontier", stds3, res3.FrontierMean).Hold();
        }

        static void display_weights(string title, SolveResult res, string[] names)
        {
            var portfolio = Enumerable.Zip(names, res.Weights, (name, wgt) => new { Ticker = name, Weight = Math.Round(wgt * 100, 3) });
            Console.WriteLine($"{title}:");
            foreach (var h in portfolio)
            {
                Console.WriteLine($"{h.Ticker}\t{h.Weight}");
            }
            Console.WriteLine($"{title} mean = {res.TangentMean} variance = {res.TangentVar}");
            Console.WriteLine();
        }

        static (double[] Qviews, double[][] Plink) create_views_and_link_matrices(
            string[] names,
            List<(string tik1, string op, string tik2, double retdif)> views)
        {
            var Q = views.Select(v => v.retdif).ToArray();
            var P = new double[views.Count, names.Length];
            var nameToIndex = new Dictionary<string, int>();
            for (int i = 0; i < names.Length; ++i) nameToIndex.Add(names[i], i);
            for (int i = 0; i < views.Count; i++)
            {
                P[i, nameToIndex[views[i].tik1]] = (views[i].op == ">") ? 1 : -1;
                P[i, nameToIndex[views[i].tik2]] = (views[i].op == ">") ? -1 : 1;
            }

            // dummy return
            return (Q, P.ToJagged());
        }

        static double port_mean(double[] W, double[] R)
        {
            return Enumerable.Zip(W, R, (w, r) => w * r).Sum();
        }

        static double port_var(double[] W, double[][] C)
        {
            var weightedSumCovarsHoldings = W.Dot(C);
            var weightedPortfolioVariance = weightedSumCovarsHoldings.Dot(W);
            return weightedPortfolioVariance;
        }

        static (double mean, double variance) port_mean_var(double[] W, double[] R, double[][] C)
        {
            return (port_mean(W, R), port_var(W, C));
        }

        static SolveResult optimize_frontier(double[] R, double[][] C, double rfrate)
        {
            var W = solve_weights(R, C, rfrate);
            var tan = port_mean_var(W, R, C);
            var front = solve_frontier_range(R, C);
            return new SolveResult
            {
                Weights = W,
                TangentMean = tan.mean,
                TangentVar = tan.variance,
                FrontierMean = front.mean,
                FrontierVar = front.variance,
            };
        }

        static (double[] mean, double[] variance) solve_frontier_range(double[] R, double[][] C)
        {
            var means = new List<double>();
            var vars = new List<double>();
            var linspace = new DoubleRange(R.Min(), R.Max());
            foreach (var r in linspace.Interval(20))
            {
                var res = solve_frontier(R, C, r);
                means.Add(res.mean);
                vars.Add(res.var);
            }
            return (means.ToArray(), vars.ToArray());
        }

        static (double mean, double var) solve_frontier(double[] R, double[][] C, double reqrate)
        {
            Func<double[], double> function = x =>
            {
                var perf = port_mean_var(x, R, C);
                var penalty = 100 * Math.Abs(perf.mean - reqrate);
                return perf.variance + penalty;
            };
            var nlf = new NonlinearObjectiveFunction(R.Length, function);
            // Sum of weights "=" to 1
            var constraints = new List<NonlinearConstraint> {
            new NonlinearConstraint(
                R.Length,x=>x.Sum(),
                ConstraintType.LesserThanOrEqualTo,
                1),
                new NonlinearConstraint(
                R.Length,x=>x.Sum(),
                ConstraintType.GreaterThanOrEqualTo,
                .999),
            };
            // Bounds for each weight 0 <= w <= 1
            for (int i = 0; i < R.Length; ++i)
            {
                var idx = i;
                constraints.Add(new NonlinearConstraint(
                R.Length, x => x[idx],
                ConstraintType.LesserThanOrEqualTo,
                1));
                constraints.Add(new NonlinearConstraint(
                R.Length, x => x[idx],
                ConstraintType.GreaterThanOrEqualTo,
                0));
            }

            var coblya = new Cobyla(nlf, constraints.ToArray());
            bool success = coblya.Minimize();
            if (success)
            {
                //coblya.Solution.Dump();
                //coblya.Solution.Sum().Dump();
                //port_var(coblya.Solution, C).Dump();
                return (reqrate, port_var(coblya.Solution, C));
            }
            else
                throw new Exception("Could not solve");

        }

        static double[] solve_weights(double[] R, double[][] C, double rfrate)
        {
            Func<double[], double> function = x =>
            {
                var perf = port_mean_var(x, R, C);
                var util = (perf.mean - rfrate) / Math.Sqrt(perf.variance);
                return util;
            };

            var nlf = new NonlinearObjectiveFunction(R.Length, function);
            // Sum of weights "=" to 1
            var constraints = new List<NonlinearConstraint> {
            new NonlinearConstraint(
                R.Length,x=>x.Sum(),
                ConstraintType.LesserThanOrEqualTo,
                1),
                new NonlinearConstraint(
                R.Length,x=>x.Sum(),
                ConstraintType.GreaterThanOrEqualTo,
                .999),
            };
            // Bounds for each weight 0 <= w <= 1
            for (int i = 0; i < R.Length; ++i)
            {
                var idx = i;
                constraints.Add(new NonlinearConstraint(
                R.Length, x => x[idx],
                ConstraintType.LesserThanOrEqualTo,
                1));
                constraints.Add(new NonlinearConstraint(
                R.Length, x => x[idx],
                ConstraintType.GreaterThanOrEqualTo,
                0));
            }

            var coblya = new Cobyla(nlf, constraints.ToArray());
            bool success = coblya.Maximize();
            if (success)
                return coblya.Solution;
            else
                throw new Exception("Could not solve");

        }

        static (double[] returns, double[][] cov) asset_returns_and_covariances(double[][] prices)
        {
            var rows = prices.Length;
            var cols = prices[0].Length;
            double[,] returns = new double[rows, cols - 1];
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < cols - 1; c++)
                {
                    var p0 = prices[r][c];
                    var p1 = prices[r][c + 1];
                    returns[r, c] = (p1 / p0) - 1; // daily return
                }
            }
            var expReturns = returns.ToJagged()
                .Select(r => Math.Pow(1 + r.Average(), 250) - 1).ToArray(); // annualized geomean

            var covars = returns.Transpose().Covariance().Multiply(250); // annualize covars
            return (expReturns, covars.ToJagged());
        }

        static (string[] names, double[][] prices, double[] caps) load_data()
        {
            // Allow for arbitrary list of symbols and get current mkt cap
            // from IEX
            var symbols = new[] { "XOM", "AAPL", "MSFT", "JNJ", "GE", "GOOG", "CVX", "PG", "WFC"};
            var caps = new double[] {403.02e9, 392.90e9, 283.60e9, 243.17e9, 236.79e9, 292.72e9,231.03e9,214.99e9,218.79e9};
            var prices = new List<double[]>();
            foreach (var ticker in symbols)
            {
                prices.Add(IEXApi.LoadPriceHistoryBlocking(ticker, "1y",APIURL,TOKEN)
                    .Select(x => x.Close)
                    .ToArray());
            }
            return (symbols, prices.ToArray(), caps);
        }

        static bool getenv(string[] args)
        {
            if (args.Length > 0)
            {
                var env = File.ReadAllLines(args[0]);
                TOKEN = env.Where(e => e.StartsWith("IEX")).Select(e => e.Split(new[] { '=' })[1]).ToArray()[0];
                APIURL = env.Where(e => e.StartsWith("API")).Select(e => e.Split(new[] { '=' })[1]).ToArray()[0];
                return true;
            }
            else
            {
                Console.WriteLine("Usage:\n");
                Console.WriteLine("finmaths [.env]\n");
                Console.WriteLine("Where .env contains lines:\n IEX=token\n API=https://cloud.iexapis.com/stable\n");
                return false;
            }
        }
   }
}
