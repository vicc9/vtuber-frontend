// Assets/Plugins/iOS/AudioSessionConfig.mm
// iOS 專用：設定 AVAudioSession，允許錄音 + 播放同時進行。
// Unity 會在 Build 時自動將此檔案加入 Xcode 專案。

#import <AVFoundation/AVFoundation.h>

extern "C" {
    void ConfigureAudioSession() {
        AVAudioSession *session = [AVAudioSession sharedInstance];
        NSError *error = nil;

        [session setCategory:AVAudioSessionCategoryPlayAndRecord
                 withOptions:AVAudioSessionCategoryOptionDefaultToSpeaker
                       error:&error];

        [session setActive:YES error:&error];

        if (error) {
            NSLog(@"[AudioSession] 設定失敗: %@", error.localizedDescription);
        } else {
            NSLog(@"[AudioSession] 設定成功：PlayAndRecord + DefaultToSpeaker");
        }
    }
}