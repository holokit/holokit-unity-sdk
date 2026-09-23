// Host-only regression bridge. This folder is excluded from Unity import.
#import "HoloKitMixedMovie.h"
#include <atomic>
static std::atomic<int> state {0}, completions {0};
static HoloKitMixedMovie* job;
extern "C" int HoloKit_TestMix(const char* source, const char* destination, int cancel) {
    @autoreleasepool {
        if (state.load() == 1) return -1;
        completions.store(0);
        state.store(1);
        // The calling thread owns this reference. The completion never mutates
        // it, so a fast error cannot race the assignment of the returned job.
        job = [HoloKitMixedMovie exportMovie:[NSURL fileURLWithPath:@(source)]
            to:[NSURL fileURLWithPath:@(destination)] completion:^(NSError* error) {
                completions.fetch_add(1);
                state.store(error ? -1 : 2);
            }];
        if (cancel) [job cancel];
        return 0;
    }
}
extern "C" int HoloKit_TestMixState() { return state.load(); }
extern "C" int HoloKit_TestMixCompletions() { return completions.load(); }
