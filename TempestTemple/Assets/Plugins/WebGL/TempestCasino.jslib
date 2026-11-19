mergeInto(LibraryManager.library, {
    TempestCasinoInit: function (target) {
        window.TempestBridge.init(UTF8ToString(target));
    },
    TempestCasinoSpin: function (target, bet) {
        window.TempestBridge.spin(UTF8ToString(target), bet);
    },
    TempestCasinoBuyBonus: function (target, bet) {
        window.TempestBridge.buyBonus(UTF8ToString(target), bet);
    },
    TempestCasinoFinished: function () {
        window.TempestBridge.finished();
    }
});
