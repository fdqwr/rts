if(NOT TARGET game-activity::game-activity)
add_library(game-activity::game-activity STATIC IMPORTED)
set_target_properties(game-activity::game-activity PROPERTIES
    IMPORTED_LOCATION "C:/Users/12345/.gradle/caches/transforms-3/112c51d2b0dcf534499cbf4813a90265/transformed/jetified-games-activity-3.0.0/prefab/modules/game-activity/libs/android.arm64-v8a/libgame-activity.a"
    INTERFACE_INCLUDE_DIRECTORIES "C:/Users/12345/.gradle/caches/transforms-3/112c51d2b0dcf534499cbf4813a90265/transformed/jetified-games-activity-3.0.0/prefab/modules/game-activity/include"
    INTERFACE_LINK_LIBRARIES ""
)
endif()

if(NOT TARGET game-activity::game-activity_static)
add_library(game-activity::game-activity_static STATIC IMPORTED)
set_target_properties(game-activity::game-activity_static PROPERTIES
    IMPORTED_LOCATION "C:/Users/12345/.gradle/caches/transforms-3/112c51d2b0dcf534499cbf4813a90265/transformed/jetified-games-activity-3.0.0/prefab/modules/game-activity_static/libs/android.arm64-v8a/libgame-activity_static.a"
    INTERFACE_INCLUDE_DIRECTORIES "C:/Users/12345/.gradle/caches/transforms-3/112c51d2b0dcf534499cbf4813a90265/transformed/jetified-games-activity-3.0.0/prefab/modules/game-activity_static/include"
    INTERFACE_LINK_LIBRARIES ""
)
endif()
