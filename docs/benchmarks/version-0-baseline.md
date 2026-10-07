# Version 0 Pure Engine Baseline

Date: 2026-10-07

## Method

- Release build, .NET SDK 10.0.401 and runtime 10.0.12
- WSL2 Linux 6.18.40.1
- AMD Ryzen 7 7700, 8 cores / 16 logical processors
- One `BTC/USD` order book
- 100,000 alternating buy and sell limit orders at the same price
- Quantity is one per order, producing 50,000 trades
- 1,000 orders are processed before measurement to warm up the JIT
- The measured path is the synchronous in-memory `OrderBook.Submit` method
- HTTP, channels, persistence, settlement, networking, and logging are excluded

## Results

| Run | Orders/sec | p50 | p95 | p99 | Managed allocation |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1 | 571,532 | 1.22 µs | 2.28 µs | 6.59 µs | 63.76 MiB |
| 2 | 593,715 | 1.19 µs | 2.19 µs | 6.27 µs | 63.76 MiB |
| 3 | 564,884 | 1.21 µs | 2.31 µs | 6.62 µs | 63.76 MiB |

Median throughput: **571,532 orders/sec**.

These results are a local baseline, not a production capacity claim. Future changes should be compared using the same workload and environment.