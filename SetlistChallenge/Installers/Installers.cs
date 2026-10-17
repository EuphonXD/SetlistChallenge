using SetlistChallenge.GameLogic;
using SetlistChallenge.UI;
using Zenject;

namespace SetlistChallenge.Installers
{
    /// <summary>游戏级别（菜单/App/游戏场景）的依赖绑定</summary>
    public class AppInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<LevelResolver>().AsSingle();
            Container.BindInterfacesAndSelfTo<RandomPicker>().AsSingle();
            Container.BindInterfacesAndSelfTo<ChallengeStarter>().AsSingle();
        }
    }

    public class MenuInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<ChallengeSettingsView>().AsSingle().NonLazy();
        }
    }

    /// <summary>游戏场景：只有当前在跑挑战时才绑定（否则完全不介入）</summary>
    public class GameInstaller : Installer
    {
        public override void InstallBindings()
        {
            if (ChallengeRun.Current == null) return;

            Plugin.Log?.Info("[SetlistChallenge] 进入挑战关卡，绑定换谱组件");

            Container.BindInterfacesAndSelfTo<AudioTimeWrapper>().AsSingle();
            Container.Bind<CustomAudioSource>().AsSingle();
            Container.BindInterfacesAndSelfTo<BeatmapLoader>().AsSingle();
            Container.BindInterfacesTo<BeatmapObjectDissolver>().AsSingle().NonLazy();
            Container.BindInterfacesTo<UnifiedATSC>().AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<BeatmapSwitcher>().AsSingle();
            Container.BindInterfacesTo<QueueProcessor>().AsSingle().NonLazy();
        }
    }
}
